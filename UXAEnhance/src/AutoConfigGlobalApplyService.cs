using System;
using UXAssist.Patches;
using UnityEngine;

namespace UXAEnhance;

internal static class AutoConfigGlobalApplyService {
    public static int Apply(AutoConfigApplyTarget target) {
        GameData gameData = GameMain.data;
        if (gameData?.factories == null || gameData.factoryCount <= 0) {
            return 0;
        }

        int changedCount = 0;
        for (int i = 0; i < gameData.factoryCount; i++) {
            PlanetFactory factory = gameData.factories[i];
            if (factory == null) {
                continue;
            }

            changedCount += ApplyToFactory(factory, target);
        }

        return changedCount;
    }

    private static int ApplyToFactory(PlanetFactory factory, AutoConfigApplyTarget target) {
        return target switch {
            AutoConfigApplyTarget.DispenserChargePower or AutoConfigApplyTarget.DispenserCourierCount => ApplyToDispensers(factory, target),
            AutoConfigApplyTarget.BattleBaseChargePower => ApplyToBattleBases(factory),
            _ => ApplyToStations(factory, target),
        };
    }

    private static int ApplyToDispensers(PlanetFactory factory, AutoConfigApplyTarget target) {
        PlanetTransport transport = factory.transport;
        if (transport?.dispenserPool == null) {
            return 0;
        }

        int changedCount = 0;
        for (int i = 1; i < transport.dispenserCursor; i++) {
            DispenserComponent dispenser = transport.dispenserPool[i];
            if (dispenser == null || dispenser.id != i) {
                continue;
            }

            if (ApplyToDispenser(factory, dispenser, target)) {
                changedCount++;
            }
        }

        return changedCount;
    }

    private static bool ApplyToDispenser(PlanetFactory factory, DispenserComponent dispenser, AutoConfigApplyTarget target) {
        switch (target) {
            case AutoConfigApplyTarget.DispenserChargePower:
                if (!TrySetWorkEnergyPerTick(factory, dispenser.pcId, (long)(5000.0 * LogisticsPatch.AutoConfigDispenserChargePower.Value + 0.5))) {
                    return false;
                }
                return true;
            case AutoConfigApplyTarget.DispenserCourierCount:
                int courierCountToFill = Math.Max(0,
                    LogisticsPatch.AutoConfigDispenserCourierCount.Value - dispenser.idleCourierCount - dispenser.workCourierCount);
                if (courierCountToFill > 0) {
                    dispenser.idleCourierCount += TakeFromPlayerPackage(KnownItemIds.Bot, courierCountToFill);
                }
                return true;
            default:
                return false;
        }
    }

    private static int ApplyToBattleBases(PlanetFactory factory) {
        if (factory.defenseSystem?.battleBases?.buffer == null) {
            return 0;
        }

        int changedCount = 0;
        BattleBaseComponent[] battleBases = factory.defenseSystem.battleBases.buffer;
        for (int i = 1; i < battleBases.Length; i++) {
            BattleBaseComponent battleBase = battleBases[i];
            if (battleBase == null || battleBase.id != i) {
                continue;
            }

            if (TrySetWorkEnergyPerTick(factory, battleBase.pcId,
                    (long)(5000.0 * LogisticsPatch.AutoConfigBattleBaseChargePower.Value + 0.5))) {
                changedCount++;
            }
        }

        return changedCount;
    }

    private static int ApplyToStations(PlanetFactory factory, AutoConfigApplyTarget target) {
        PlanetTransport transport = factory.transport;
        if (transport?.stationPool == null) {
            return 0;
        }

        int changedCount = 0;
        for (int i = 1; i < transport.stationCursor; i++) {
            StationComponent station = transport.stationPool[i];
            if (station == null || station.id != i || station.isCollector) {
                continue;
            }

            if (station.isVeinCollector) {
                if (ApplyToVeinCollector(factory, station, target)) {
                    changedCount++;
                }
                continue;
            }

            if (!station.isStellar && ApplyToPls(factory, station, target)) {
                changedCount++;
            } else if (station.isStellar && ApplyToIls(factory, station, target)) {
                changedCount++;
            }
        }

        return changedCount;
    }

    private static bool ApplyToVeinCollector(PlanetFactory factory, StationComponent station, AutoConfigApplyTarget target) {
        switch (target) {
            case AutoConfigApplyTarget.VeinCollectorHarvestSpeed:
                if (!TryGetMinerId(factory, station, out int minerId)) {
                    return false;
                }
                factory.factorySystem.minerPool[minerId].speed = 10000 + LogisticsPatch.AutoConfigVeinCollectorHarvestSpeed.Value * 1000;
                return true;
            case AutoConfigApplyTarget.VeinCollectorMinPilerValue:
                station.pilerCount = LogisticsPatch.AutoConfigVeinCollectorMinPilerValue.Value;
                return true;
            default:
                return false;
        }
    }

    private static bool ApplyToPls(PlanetFactory factory, StationComponent station, AutoConfigApplyTarget target) {
        switch (target) {
            case AutoConfigApplyTarget.PlsChargePower:
                return TrySetWorkEnergyPerTick(factory, station.pcId,
                    (long)(50000.0 * LogisticsPatch.AutoConfigPLSChargePower.Value + 0.5));
            case AutoConfigApplyTarget.PlsMaxTripDrone:
                station.tripRangeDrones = Math.Cos(LogisticsPatch.AutoConfigPLSMaxTripDrone.Value / 180.0 * Math.PI);
                return true;
            case AutoConfigApplyTarget.PlsDroneMinDeliver:
                station.deliveryDrones = DeliveryPercent(LogisticsPatch.AutoConfigPLSDroneMinDeliver.Value);
                return true;
            case AutoConfigApplyTarget.PlsMinPilerValue:
                station.pilerCount = LogisticsPatch.AutoConfigPLSMinPilerValue.Value;
                return true;
            case AutoConfigApplyTarget.PlsDroneCount:
                FillStationDrones(station, LogisticsPatch.AutoConfigPLSDroneCount.Value);
                return true;
            default:
                return false;
        }
    }

    private static bool ApplyToIls(PlanetFactory factory, StationComponent station, AutoConfigApplyTarget target) {
        switch (target) {
            case AutoConfigApplyTarget.IlsChargePower:
                return TrySetWorkEnergyPerTick(factory, station.pcId,
                    (long)(250000.0 * LogisticsPatch.AutoConfigILSChargePower.Value + 0.5));
            case AutoConfigApplyTarget.IlsMaxTripDrone:
                station.tripRangeDrones = Math.Cos(LogisticsPatch.AutoConfigILSMaxTripDrone.Value / 180.0 * Math.PI);
                return true;
            case AutoConfigApplyTarget.IlsMaxTripShip:
                station.tripRangeShips = LogisticsPatch.AutoConfigILSMaxTripShip.Value switch {
                    <= 20 => LogisticsPatch.AutoConfigILSMaxTripShip.Value,
                    <= 40 => LogisticsPatch.AutoConfigILSMaxTripShip.Value * 2 - 20,
                    _ => 10000,
                } * 2400000.0;
                return true;
            case AutoConfigApplyTarget.IlsWarperDistance:
                station.warpEnableDist = LogisticsPatch.AutoConfigILSWarperDistance.Value switch {
                    <= 7 => LogisticsPatch.AutoConfigILSWarperDistance.Value * 0.5 - 0.5,
                    <= 16 => LogisticsPatch.AutoConfigILSWarperDistance.Value - 4.0,
                    <= 20 => LogisticsPatch.AutoConfigILSWarperDistance.Value * 2 - 20.0,
                    _ => 60.0,
                } * 40000.0;
                return true;
            case AutoConfigApplyTarget.IlsDroneMinDeliver:
                station.deliveryDrones = DeliveryPercent(LogisticsPatch.AutoConfigILSDroneMinDeliver.Value);
                return true;
            case AutoConfigApplyTarget.IlsShipMinDeliver:
                station.deliveryShips = DeliveryPercent(LogisticsPatch.AutoConfigILSShipMinDeliver.Value);
                return true;
            case AutoConfigApplyTarget.IlsMinPilerValue:
                station.pilerCount = LogisticsPatch.AutoConfigILSMinPilerValue.Value;
                return true;
            case AutoConfigApplyTarget.IlsDroneCount:
                FillStationDrones(station, LogisticsPatch.AutoConfigILSDroneCount.Value);
                return true;
            case AutoConfigApplyTarget.IlsShipCount:
                FillStationShips(station, LogisticsPatch.AutoConfigILSShipCount.Value);
                return true;
            default:
                return false;
        }
    }

    private static int DeliveryPercent(int value) {
        return value == 0 ? 1 : value * 10;
    }

    private static void FillStationDrones(StationComponent station, int targetCount) {
        int droneCountToFill = Math.Max(0, targetCount - station.idleDroneCount - station.workDroneCount);
        if (droneCountToFill > 0) {
            station.idleDroneCount += TakeFromPlayerPackage(KnownItemIds.Drone, droneCountToFill);
        }
    }

    private static void FillStationShips(StationComponent station, int targetCount) {
        int shipCountToFill = Math.Max(0, targetCount - station.idleShipCount - station.workShipCount);
        if (shipCountToFill > 0) {
            station.idleShipCount += TakeFromPlayerPackage(KnownItemIds.Ship, shipCountToFill);
        }
    }

    private static int TakeFromPlayerPackage(int itemId, int count) {
        Player player = GameMain.data?.mainPlayer;
        if (player?.package == null || count <= 0) {
            return 0;
        }

        return player.package.TakeItem(itemId, count, out _);
    }

    private static bool TryGetMinerId(PlanetFactory factory, StationComponent station, out int minerId) {
        minerId = 0;
        if (factory?.factorySystem?.minerPool == null) {
            return false;
        }

        minerId = station.minerId;
        if (minerId <= 0 && station.entityId > 0 && station.entityId < factory.entityCursor) {
            ref EntityData entity = ref factory.entityPool[station.entityId];
            if (entity.id == station.entityId) {
                minerId = entity.minerId;
            }
        }

        if (minerId <= 0 || minerId >= factory.factorySystem.minerCursor) {
            return false;
        }

        return factory.factorySystem.minerPool[minerId].id == minerId;
    }

    private static bool TrySetWorkEnergyPerTick(PlanetFactory factory, int pcId, long value) {
        if (factory?.powerSystem?.consumerPool == null || pcId <= 0 || pcId >= factory.powerSystem.consumerCursor) {
            return false;
        }

        factory.powerSystem.consumerPool[pcId].workEnergyPerTick = value;
        return true;
    }
}
