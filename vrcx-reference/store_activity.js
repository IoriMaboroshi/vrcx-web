import { defineStore } from 'pinia';
import { ref } from 'vue';

import { database } from '../services/database';
import { mergeSessions } from '../shared/utils/activityEngine.js';
import { runActivityWorkerTask } from '../workers/activityWorkerRunner.js';

const snapshotMap = new Map();
const inFlightJobs = new Map();
const workerCall = runActivityWorkerTask;
const MAX_SNAPSHOT_ENTRIES = 12;
let deferredWriteQueue = Promise.resolve();
const FULL_CACHE_BATCH_DAYS = 30;
const FULL_CACHE_MAX_DAYS = 3650; // vrchat max days??

function deferWrite(task) {
    const run = () => {
        deferredWriteQueue = deferredWriteQueue
            .catch(() => {})
            .then(task)
            .catch((error) => {
                console.error('[Activity] deferred write failed:', error);
            });
        return deferredWriteQueue;
    };
    if (typeof requestIdleCallback === 'function') {
        requestIdleCallback(run);
        return;
    }
    setTimeout(run, 0);
}

function createSnapshot(userId, isSelf) {
    return {
        userId,
        isSelf,
        sync: {
            userId,
            updatedAt: '',
            isSelf,
            sourceLastCreatedAt: '',
            pendingSessionStartAt: null,
            cachedRangeDays: 0
        },
        sessions: [],
        activityViews: new Map(),
        overlapViews: new Map()
    };
}

function getSnapshot(userId, isSelf) {
    let snapshot = snapshotMap.get(userId);
    if (!snapshot) {
        snapshot = createSnapshot(userId, isSelf);
        snapshotMap.set(userId, snapshot);
    } else if (typeof isSelf === 'boolean') {
        snapshot.isSelf = isSelf;
        snapshot.sync.isSelf = isSelf;
    }
    touchSnapshot(userId, snapshot);
    pruneSnapshots();
    return snapshot;
}

function touchSnapshot(userId, snapshot) {
    snapshotMap.delete(userId);
    snapshotMap.set(userId, snapshot);
}

function pruneSnapshots() {
    if (snapshotMap.size <= MAX_SNAPSHOT_ENTRIES) {
        return;
    }

    for (const [userId] of snapshotMap) {
        if (isUserInFlight(userId)) {
            continue;
        }
        snapshotMap.delete(userId);
        if (snapshotMap.size <= MAX_SNAPSHOT_ENTRIES) {
            break;
        }
    }
}

function isUserInFlight(userId) {
    for (const key of inFlightJobs.keys()) {
        if (key.startsWith(`${userId}:`)) {
            return true;
        }
    }
    return false;
}

function clearDerivedViews(snapshot) {
    snapshot.activityViews.clear();
    snapshot.overlapViews.clear();
}

function overlapExcludeKey(excludeHours) {
    if (!excludeHours?.enabled) {
        return '';
    }
    return `${excludeHours.startHour}-${excludeHours.endHour}`;
}

function pairCursor(leftCursor, rightCursor) {
    return `${leftCursor || ''}|${rightCursor || ''}`;
}

export const useActivityStore = defineStore('Activity', () => {
    const fullCacheReady = ref(false);
    let fullCacheBuildRunning = false;
    let fullCacheBuildAborted = false;

    const userActivity = ref({
        lastLoadedUserId: '',
        activeRequestId: 0,
        activeOverlapRequestId: 0,
        activeTopWorldsRequestId: 0
    });

    async function getCache(userId, isSelf = false) {
        const snapshot = await hydrateSnapshot(userId, isSelf);
        return {
            userId: snapshot.userId,
            isSelf: snapshot.isSelf,
            updatedAt: snapshot.sync.updatedAt,
            sourceLastCreatedAt: snapshot.sync.sourceLastCreatedAt,
            pendingSessionStartAt: snapshot.sync.pendingSessionStartAt,
            cachedRangeDays: snapshot.sync.cachedRangeDays,
            sessions: snapshot.sessions
        };
    }

    function getCachedDays(userId) {
        return snapshotMap.get(userId)?.sync.cachedRangeDays || 0;
    }

    function isRefreshing(userId) {
        for (const key of inFlightJobs.keys()) {
            if (key.startsWith(`${userId}:`)) {
                return true;
            }
        }
        return false;
    }

    async function loadActivity(
        userId,
        {
            isSelf = false,
            rangeDays = 30,
            normalizeConfig,
            dayLabels,
            forceRefresh = false
        }
    ) {
        const snapshot = await ensureSnapshot(userId, {
            isSelf,
            rangeDays,
            forceRefresh
        });
        const cacheKey = String(rangeDays);
        const currentCursor = snapshot.sync.sourceLastCreatedAt || '';

        let view = snapshot.activityViews.get(cacheKey);
        if (!forceRefresh && view?.builtFromCursor === currentCursor) {
            return buildActivityResponse(snapshot, view);
        }

        if (!forceRefresh) {
            const persisted = await database.getActivityBucketCacheV2({
                ownerUserId: userId,
                rangeDays,
                viewKind: database.ACTIVITY_VIEW_KIND.ACTIVITY
            });
            if (persisted?.builtFromCursor === currentCursor) {
                view = {
                    ...persisted.summary,
                    rawBuckets: persisted.rawBuckets,
                    normalizedBuckets: persisted.normalizedBuckets,
                    builtFromCursor: persisted.builtFromCursor,
                    builtAt: persisted.builtAt
                };
                snapshot.activityViews.set(cacheKey, view);
                return buildActivityResponse(snapshot, view);
            }
        }

        const computed = await workerCall('computeActivityView', {
            sessions: snapshot.sessions,
            dayLabels,
            rangeDays,
            normalizeConfig
        });
        view = {
            ...computed,
            builtFromCursor: currentCursor,
            builtAt: new Date().toISOString()
        };
        snapshot.activityViews.set(cacheKey, view);
        deferWrite(() =>
            database.upsertActivityBucketCacheV2({
                ownerUserId: userId,
                rangeDays,
                viewKind: database.ACTIVITY_VIEW_KIND.ACTIVITY,
                builtFromCursor: currentCursor,
                rawBuckets: view.rawBuckets,
                normalizedBuckets: view.normalizedBuckets,
                summary: {
                    peakDay: view.peakDay,
                    peakTime: view.peakTime,
                    filteredEventCount: view.filteredEventCount
                },
                builtAt: view.builtAt
            })
        );
        return buildActivityResponse(snapshot, view);
    }

    async function loadOverlap(
        currentUserId,
        targetUserId,
        {
            rangeDays = 30,
            dayLabels,
            normalizeConfig,
            excludeHours,
            forceRefresh = false
        }
    ) {
        const [selfSnapshot, targetSnapshot] = await Promise.all([
            ensureSnapshot(currentUserId, {
                isSelf: true,
                rangeDays,
                forceRefresh
            }),
            ensureSnapshot(targetUserId, {
                isSelf: false,
                rangeDays,
                forceRefresh
            })
        ]);

        const excludeKey = overlapExcludeKey(excludeHours);
        const cacheKey = `${targetUserId}:${rangeDays}:${excludeKey}`;
        const cursor = pairCursor(
            selfSnapshot.sync.sourceLastCreatedAt,
            targetSnapshot.sync.sourceLastCreatedAt
        );

        let view = targetSnapshot.overlapViews.get(cacheKey);
        if (!forceRefresh && view?.builtFromCursor === cursor) {
            return view;
        }

        if (!forceRefresh) {
            const persisted = await database.getActivityBucketCacheV2({
                ownerUserId: currentUserId,
                targetUserId,
                rangeDays,
                viewKind: database.ACTIVITY_VIEW_KIND.OVERLAP,
                excludeKey
            });
            if (persisted?.builtFromCursor === cursor) {
                view = {
                    ...persisted.summary,
                    rawBuckets: persisted.rawBuckets,
                    normalizedBuckets: persisted.normalizedBuckets,
                    builtFromCursor: persisted.builtFromCursor,
                    builtAt: persisted.builtAt
                };
                targetSnapshot.overlapViews.set(cacheKey, view);
                return view;
            }
        }

        view = await workerCall('computeOverlapView', {
            selfSessions: selfSnapshot.sessions,
            targetSessions: targetSnapshot.sessions,
            dayLabels,
            rangeDays,
            excludeHours: excludeHours?.enabled ? excludeHours : null,
            normalizeConfig
        });
        view = {
            ...view,
            builtFromCursor: cursor,
            builtAt: new Date().toISOString()
        };
        targetSnapshot.overlapViews.set(cacheKey, view);
        deferWrite(() =>
            database.upsertActivityBucketCacheV2({
                ownerUserId: currentUserId,
                targetUserId,
                rangeDays,
                viewKind: database.ACTIVITY_VIEW_KIND.OVERLAP,
                excludeKey,
                builtFromCursor: cursor,
                rawBuckets: view.rawBuckets,
                normalizedBuckets: view.normalizedBuckets,
                summary: {
                    overlapPercent: view.overlapPercent,
                    bestOverlapTime: view.bestOverlapTime
                },
                builtAt: view.builtAt
            })
        );
        return view;
    }

    async function loadTopWorlds(
        userId,
        { rangeDays = 30, limit = 5, sortBy = 'time', excludeWorldId = '' }
    ) {
        void userId;
        return database.getMyTopWorlds(
            rangeDays,
            limit,
            sortBy,
            excludeWorldId
        );
    }

    async function refreshActivity(userId, options) {
        return loadActivity(userId, { ...options, forceRefresh: true });
    }

    async function loadActivityView({
        userId,
        isSelf = false,
        rangeDays = 30,
        dayLabels,
        forceRefresh = false
    }) {
        const response = await loadActivity(userId, {
            isSelf,
            rangeDays,
            dayLabels,
            forceRefresh,
            normalizeConfig: pickActivityNormalizeConfig(isSelf, rangeDays)
        });
        return {
            hasAnyData: response.sessions.length > 0,
            filteredEventCount: response.view.filteredEventCount,
            peakDay: response.view.peakDay,
            peakTime: response.view.peakTime,
            rawBuckets: response.view.rawBuckets,
            normalizedBuckets: response.view.normalizedBuckets
        };
    }

    async function loadOverlapView({
        currentUserId,
        targetUserId,
        rangeDays = 30,
        dayLabels,
        excludeHours,
        forceRefresh = false
    }) {
        const response = await loadOverlap(currentUserId, targetUserId, {
            rangeDays,
            dayLabels,
            excludeHours,
            forceRefresh,
            normalizeConfig: pickOverlapNormalizeConfig(rangeDays)
        });
        return {
            hasOverlapData: response.rawBuckets.some((value) => value > 0),
            overlapPercent: response.overlapPercent,
            bestOverlapTime: response.bestOverlapTime,
            rawBuckets: response.rawBuckets,
            normalizedBuckets: response.normalizedBuckets
        };
    }

    async function loadTopWorldsView({
        userId,
        rangeDays = 30,
        limit = 5,
        sortBy = 'time',
        excludeWorldId = ''
    }) {
        return loadTopWorlds(userId, {
            rangeDays,
            limit,
            sortBy,
            excludeWorldId,
            isSelf: true
        });
    }

    function invalidateUser(userId) {
        if (!userId) {
            return;
        }
        snapshotMap.delete(userId);
    }

    async function startFullCacheBuild(userId) {
        if (fullCacheBuildRunning) {
            return;
        }
        fullCacheBuildRunning = true;
        fullCacheBuildAborted = false;
        fullCacheReady.value = false;

        try {
            const snapshot = await hydrateSnapshot(userId, true);

            if (!snapshot.sync.updatedAt) {
                await fullRefresh(snapshot, 90);
            }

            const currentDays = snapshot.sync.cachedRangeDays || 90;

            const probeItems = await database.getActivitySourceSliceV2({
                userId,
                isSelf: true,
                fromDays: FULL_CACHE_MAX_DAYS,
                toDays: currentDays
            });

            if (probeItems.length === 0) {
                fullCacheReady.value = true;
                fullCacheBuildRunning = false;
                return;
            }

            const earliestDate = new Date(probeItems[0].created_at);
            const totalDays = Math.ceil(
                (Date.now() - earliestDate.getTime()) / 86400000
            );

            let targetDays = currentDays;
            while (targetDays < totalDays && !fullCacheBuildAborted) {
                targetDays = Math.min(
                    targetDays + FULL_CACHE_BATCH_DAYS,
                    totalDays
                );
                const nextTarget = targetDays;

                await new Promise((resolve) => {
                    const callback = async () => {
                        try {
                            await expandRange(snapshot, nextTarget);
                        } catch (error) {
                            console.error(
                                '[Activity] full cache batch failed:',
                                error
                            );
                        }
                        resolve();
                    };
                    if (typeof requestIdleCallback === 'function') {
                        requestIdleCallback(callback);
                    } else {
                        setTimeout(callback, 0);
                    }
                });
            }

            if (!fullCacheBuildAborted) {
                fullCacheReady.value = true;
            }
        } catch (error) {
            console.error('[Activity] full cache build error:', error);
        } finally {
            fullCacheBuildRunning = false;
        }
    }

    function stopFullCacheBuild() {
        fullCacheBuildAborted = true;
    }

    /**
     * @returns {Promise<Array<{date: string, totalMs: number}>>}
     */
    async function getDailySummary(userId) {
        const snapshot = await hydrateSnapshot(userId, true);
        if (snapshot.sessions.length === 0) {
            return [];
        }

        const rangeStartMs = snapshot.sessions[0].start;
        const rangeEndMs = Date.now();

        const result = await workerCall('computeDailySummary', {
            sessions: snapshot.sessions,
            rangeStartMs,
            rangeEndMs
        });
        return result.dailySummary;
    }

    return {
        fullCacheReady,
        getCache,
        getCachedDays,
        isRefreshing,
