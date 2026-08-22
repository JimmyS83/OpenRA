#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Grants a shield with its own health pool. Main health pool is unaffected by damage until the shield is broken.")]
	public class ShieldedInfo : PausableConditionalTraitInfo
	{
		[Desc("The strength of the shield (amount of damage it will absorb).")]
		public readonly int MaxStrength = 1000;

		[Desc("The strength of the shield (amount of damage it will absorb) in percentage of health.")]
		public readonly int MaxPercentageStrength = 0;

		[Desc("Strength of the shield when the trait is enabled.")]
		public readonly int InitialStrength = 1000;

		[Desc("The strength of the shield (amount of damage it will absorb).")]
		public readonly int InitialPercentageStrength = 0;

		[Desc("Delay in ticks before shield regenerate for the first time after trait is enabled.")]
		public readonly int InitialRegenDelay = 0;

		[Desc("Delay in ticks after absorbing damage before the shield will regenerate.")]
		public readonly int DamageRegenDelay = 0;

		[Desc("Amount to recharge at each interval.")]
		public readonly int RegenAmount = 0;

		[Desc("Number of ticks between recharging.")]
		public readonly int RegenInterval = 25;

		[Desc("Block the remaining damage after shield breaks.")]
		public readonly bool BlockExcessDamage = false;

		[Desc("Damage types that ignore this shield.")]
		public readonly BitSet<DamageType> IgnoreShieldDamageTypes = default;

		[GrantedConditionReference]
		[Desc("Condition to grant when shields are active.")]
		public readonly string ShieldsUpCondition = null;

		[Desc("Hides selection bar when shield is at max strength.")]
		public readonly bool HideBarWhenFull = false;

		public readonly bool ShowSelectionBar = true;
		public readonly Color SelectionBarColor = Color.FromArgb(128, 200, 255);

		public override object Create(ActorInitializer init) { return new Shielded(init, this); }
	}

	public class Shielded : PausableConditionalTrait<ShieldedInfo>, ITick, ISync, ISelectionBar, IDamageModifier, INotifyDamage,
		ITransformActorInitModifier
	{
		int conditionToken = Actor.InvalidConditionToken;
		readonly Actor self;

		[VerifySync]
		public int Strength;
		public int MaxStrength;
		public int InitialStrength;
		int ticks;

		IHealth health;

		// ⭐ THE SHIELD IS CARRIED AS A PERCENTAGE, exactly the way health is.
		//
		// `Transform.cs` sends `health.HP * 100 / health.MaxHP` in a HealthInit, which is why a
		// damaged MCV deploys into an equally damaged construction yard: a percentage is the
		// only quantity that means anything across two actors with different maxima. The shield
		// had no such handling, so a transform rebuilt it from InitialStrength -- and because
		// TraitEnabled did the same, toggling the trait refilled the bar outright.
		//
		// null until either a transform hands one over or the trait is disabled, so a genuine
		// first grant still uses InitialStrength.
		int? retainedPercentage;

		public Shielded(ActorInitializer init, ShieldedInfo info)
			: base(info)
		{
			self = init.Self;
			retainedPercentage = init.GetOrDefault<ShieldInit>()?.Value;
		}

		void ITransformActorInitModifier.ModifyTransformActorInit(Actor self, TypeDictionary init)
		{
			if (MaxStrength <= 0)
				return;

			init.Add(new ShieldInit((int)(Strength * 100L / MaxStrength)));
		}

		// The strength a (re)grant should hand over: the retained fraction of the CURRENT
		// maximum when there is one, otherwise the configured initial strength.
		int GrantedStrength()
		{
			return retainedPercentage != null
				? ((int)(MaxStrength * (long)retainedPercentage.Value / 100)).Clamp(0, MaxStrength)
				: InitialStrength;
		}

		protected override void Created(Actor self)
		{
			base.Created(self);
			health = self.TraitOrDefault<IHealth>();
			MaxStrength = Info.MaxStrength + Info.MaxPercentageStrength * health.MaxHP / 100;
			InitialStrength = Info.InitialStrength + Info.InitialPercentageStrength * health.MaxHP / 100;
			Strength = GrantedStrength();
			ticks = Info.InitialRegenDelay;
		}

		void ITick.Tick(Actor self)
		{
			Regenerate(self);
		}

		protected void Regenerate(Actor self)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			if (Strength == MaxStrength)
				return;

			if (--ticks > 0)
				return;

			Strength += Info.RegenAmount;

			if (Strength > MaxStrength)
				Strength = MaxStrength;

			if (Strength > 0 && conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(Info.ShieldsUpCondition);

			ticks = Info.RegenInterval;
		}

		public void Regenerate(Actor self, int amount)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			Strength += amount;

			if (Strength > 0 && conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(Info.ShieldsUpCondition);

			if (Strength <= 0 && conditionToken != Actor.InvalidConditionToken)
			{
				Strength = 0;
				conditionToken = self.RevokeCondition(conditionToken);
			}
		}

		void INotifyDamage.Damaged(Actor self, AttackInfo e)
		{
			if (IsTraitDisabled)
				return;

			if (Strength == 0 || e.Damage.Value == 0 || e.Attacker == self)
				return;

			if (e.Damage.Value < 0 || (!Info.IgnoreShieldDamageTypes.IsEmpty && e.Damage.DamageTypes.Overlaps(Info.IgnoreShieldDamageTypes)))
				return;

			if (ticks < Info.DamageRegenDelay)
				ticks = Info.DamageRegenDelay;

			var damageAmt = Convert.ToInt32(e.Damage.Value / 0.01);
			var damageTypes = e.Damage.DamageTypes;
			var excessDamage = damageAmt - Strength;
			Strength = Math.Max(Strength - damageAmt, 0);

			if (health != null)
			{
				var absorbedDamage = new Damage(-e.Damage.Value, damageTypes, e.Damage.ProjectileType);
				health.InflictDamage(self, self, absorbedDamage, true);
			}

			if (Strength == 0 && conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);

			if (excessDamage > 0 && !Info.BlockExcessDamage)
			{
				var hullDamage = new Damage(excessDamage, damageTypes, e.Damage.ProjectileType);

				health?.InflictDamage(self, e.Attacker, hullDamage, true);
			}
		}

		float ISelectionBar.GetValue()
		{
			if (IsTraitDisabled || !Info.ShowSelectionBar || Strength == 0 || (Strength == MaxStrength && Info.HideBarWhenFull))
				return 0;

			var selected = self.World.Selection.Contains(self);
			var rollover = self.World.Selection.RolloverContains(self);
			var regularWorld = self.World.Type == WorldType.Regular;
			var statusBars = Game.Settings.Game.StatusBars;

			var displayHealth = selected || rollover || (regularWorld && statusBars == StatusBarsType.AlwaysShow)
				|| (regularWorld && statusBars == StatusBarsType.DamageShow && Strength < MaxStrength);

			if (!displayHealth)
				return 0;

			return (float)Strength / MaxStrength;
		}

		bool ISelectionBar.DisplayWhenEmpty { get { return false; } }

		Color ISelectionBar.GetColor() { return Info.SelectionBarColor; }

		int IDamageModifier.GetDamageModifier(Actor attacker, Damage damage)
		{
			return IsTraitDisabled || Strength == 0 || (!Info.IgnoreShieldDamageTypes.IsEmpty && damage.DamageTypes.Overlaps(Info.IgnoreShieldDamageTypes)) ? 100 : 1;
		}

		protected override void TraitEnabled(Actor self)
		{
			ticks = Info.InitialRegenDelay;

			// ⛔ NOT `InitialStrength`. That refilled the bar every time the trait switched on,
			// so anything a player can toggle -- a deploy, an upgrade flickering -- was an
			// unlimited-shield exploit: disable, enable, full bar.
			Strength = GrantedStrength();

			if (conditionToken == Actor.InvalidConditionToken && Strength > 0)
				conditionToken = self.GrantCondition(Info.ShieldsUpCondition);
		}

		protected override void TraitDisabled(Actor self)
		{
			// Remember the fraction that survived, so switching back on restores what was left
			// rather than a full shield.
			retainedPercentage = MaxStrength > 0 ? (int)(Strength * 100L / MaxStrength) : 0;

			if (conditionToken == Actor.InvalidConditionToken)
				return;

			conditionToken = self.RevokeCondition(conditionToken);
		}
	}

	// Percentage (0-100) of the shield pool an actor starts with, mirroring HealthInit's
	// contract. Emitted by Transform via ITransformActorInitModifier so a transformed actor
	// keeps the FRACTION of shield it had instead of being handed a fresh one.
	public class ShieldInit : ValueActorInit<int>, ISingleInstanceInit
	{
		public ShieldInit(int value)
			: base(value) { }
	}
}
