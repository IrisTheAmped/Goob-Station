// SPDX-FileCopyrightText: 2024 TGRCDev <tgrc@tgrc.dev>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Marcus F <marcus2008stoke@gmail.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 SX_7 <sn1.test.preria.2002@gmail.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
// SPDX-FileCopyrightText: 2025 thebiggestbruh <199992874+thebiggestbruh@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 thebiggestbruh <marcus2008stoke@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.Changeling;
using Content.Goobstation.Shared.Changeling;
using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Goobstation.Changeling;

[TestFixture]
public sealed class ChangelingArmorTest
{
    [Test]
    [TestCase("ActionToggleChitinousArmor", "ChangelingClothingOuterArmor", "ChangelingClothingHeadHelmet")]
    public async Task TestChangelingFullArmor(string actionProto, string outerProto, string helmetProto)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            // This makes it take like 3 minutes, twice.
            // Dirty = true,
            // InLobby = false,
            // DummyTicker = false,
        });

        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var ticker = server.System<GameTicker>();
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();

        var lingSys = entMan.System<ChangelingSystem>();
        var antagSys = entMan.System<AntagSelectionSystem>();
        var mindSys = entMan.System<MindSystem>();
        var actionSys = entMan.System<ActionsSystem>();
        var invSys = entMan.System<InventorySystem>();

        // Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

        EntityUid urist = EntityUid.Invalid;
        Goobstation.Shared.Changeling.Components.ChangelingIdentityComponent changelingIdentity = null;
        Entity<InstantActionComponent> armorAction = (EntityUid.Invalid, null);

        await server.WaitPost(() =>
        {
            // Spawn a urist
            urist = entMan.SpawnEntity("MobHuman", testMap.GridCoords);

            // Make urist a changeling
            changelingIdentity = entMan.AddComponent<Goobstation.Shared.Changeling.Components.ChangelingIdentityComponent>(urist);
            changelingIdentity.TotalAbsorbedEntities += 10;
            changelingIdentity.MaxChemicals = 1000;
            changelingIdentity.Chemicals = 1000;

            // Give urist chitinous armor action
            var armorActionEnt = actionSys.AddAction(urist, actionProto);
            armorAction = (armorActionEnt.Value, entMan.GetComponent<InstantActionComponent>(armorActionEnt.Value));
            actionSys.SetUseDelay(armorAction, null);

            // Armor up
            actionSys.PerformAction(urist, null, armorAction, armorAction.Comp, armorAction.Comp.BaseEvent, timing.CurTime);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(invSys.TryGetSlotEntity(urist, "outerClothing", out var outerClothing), Is.True);
            Assert.That(outerClothing, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(outerClothing.Value).EntityPrototype!.ID, Is.EqualTo(outerProto));

            Assert.That(invSys.TryGetSlotEntity(urist, "head", out var head));
            Assert.That(head, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(head.Value).EntityPrototype!.ID, Is.EqualTo(helmetProto));
        });

        await server.WaitPost(() =>
        {
            // Armor down
            actionSys.PerformAction(urist, null, armorAction, armorAction.Comp, armorAction.Comp.BaseEvent, timing.CurTime);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(invSys.TryGetSlotEntity(urist, "outerClothing", out var outerClothing), Is.False);
                Assert.That(entMan.TryGetComponent<MetaDataComponent>(outerClothing, out var meta), Is.False);
                Assert.That(meta?.EntityPrototype, Is.Null);
            });

            Assert.Multiple(() =>
            {
                Assert.That(invSys.TryGetSlotEntity(urist, "head", out var head), Is.False);
                Assert.That(entMan.TryGetComponent<MetaDataComponent>(head, out var meta), Is.False);
                Assert.That(meta?.EntityPrototype, Is.Null);
            });
        });

        const string mercHelmet = "ClothingHeadHelmetMerc";

        await server.WaitPost(() =>
        {
            // Equip helmet
            var helm = entMan.SpawnEntity(mercHelmet, testMap.GridCoords);
            Assert.That(invSys.TryEquip(urist, helm, "head", force: true));

            // Try to armor up, should fail due to helmet and not equip anything
            actionSys.PerformAction(urist, null, armorAction, armorAction.Comp, armorAction.Comp.BaseEvent, timing.CurTime);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(invSys.TryGetSlotEntity(urist, "outerClothing", out var outerClothing), Is.False);
                Assert.That(entMan.TryGetComponent<MetaDataComponent>(outerClothing, out var meta), Is.False);
                Assert.That(meta?.EntityPrototype, Is.Null);
            });

            Assert.That(invSys.TryGetSlotEntity(urist, "head", out var head));
            Assert.That(head, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(head.Value).EntityPrototype!.ID, Is.EqualTo(mercHelmet));
        });

        await server.WaitPost(() => entMan.DeleteEntity(urist));

        await pair.CleanReturnAsync();
    }
}
