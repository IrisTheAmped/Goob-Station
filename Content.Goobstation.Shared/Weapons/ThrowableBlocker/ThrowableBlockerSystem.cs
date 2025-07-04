// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aviu00 <93730715+Aviu00@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Goobstation.Shared.Weapons.ThrowableBlocker;

public sealed class ThrowableBlockerSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.SubscribeWithRelay<ReflectComponent, ThrowHitByEvent>(
            OnThrowHit, baseEvent: false);
    }

    private void OnThrowHit(Entity<ReflectComponent> ent, ref ThrowHitByEvent args)
    {
        var thrown = args.Thrown;

        if (!TryComp(thrown, out ThrowableBlockedComponent? blockedComp))
            return;

        var blocked = _hands
            .EnumerateHeld(ent)
            .FirstOrDefault(e => HasComp<ThrowableBlockerComponent>(e) && _toggle.IsActivated(e));

        if (blocked == default)
            return;

        var blockerComp = Comp<ThrowableBlockerComponent>(blocked);

        args.Handled = true;

        if (_net.IsServer)
        {
            _popup.PopupEntity(Loc.GetString("throwable-blocker-blocked"), ent);
            _audio.PlayPvs(blockerComp.BlockSound, ent);
        }

        switch (blockedComp.Behavior)
        {
            case BlockBehavior.KnockOff:
                Knockoff(thrown);
                break;
            case BlockBehavior.Damage:
                Knockoff(thrown);
                _damageable.TryChangeDamage(thrown, blockedComp.Damage);
                break;
            case BlockBehavior.Destroy when _net.IsServer:
                Del(thrown);
                break;
        }
    }

    private void Knockoff(EntityUid entity)
    {
        if (!TryComp(entity, out PhysicsComponent? physics) || physics.LinearVelocity.LengthSquared() <= 0f)
            return;

        _physics.SetLinearVelocity(entity, -physics.LinearVelocity / 3f, body: physics);
    }
}
