using Godot;

public partial class RaidMapDemo
{
	private void UpdateRoomSimulation(float delta)
	{
		if (_inHideout || _runEnded)
		{
			return;
		}

		if (_roomDirty)
		{
			RebuildRoomEnemies();
			_roomDirty = false;
		}

		RoomUnit hero = FindHeroUnit();
		if (hero == null)
		{
			return;
		}

		UpdateRoomImpactEffects(delta);
		TickRoomUnitState(hero, delta);
		if (!hero.IsAlive)
		{
			if (!HasLivingPlayerRoomUnits())
			{
				_playerHp = 0;
				_runBackpack.Clear();
				_overflowBackpackItems.Clear();
				_hasDraggedBackpackItem = false;
				_draggedBackpackItem = null;
				_runEnded = true;
				_runFailed = true;
				_selectedContainerIndex = -1;
				_showSearchConfirm = false;
				_status = "全队失去作战能力。";
				return;
			}
		}

		if (hero.IsAlive && hero.HitPauseTime > 0f)
		{
			hero.Facing = hero.Facing == Vector2.Zero ? Vector2.Right : hero.Facing.Normalized();
		}
		else if (hero.IsAlive && hero.KnockbackTime > 0f)
		{
			AdvanceKnockback(hero, delta);
		}
		else if (hero.IsAlive && (hero.StaggerTime > 0f || hero.AttackWindupTime > 0f || hero.RecoveryTime > 0f))
		{
			hero.Facing = hero.Facing == Vector2.Zero ? Vector2.Right : hero.Facing.Normalized();
		}
		else if (hero.IsAlive && _heroHasMoveTarget)
		{
			Vector2 toTarget = _heroMoveTarget - hero.Position;
			if (toTarget.Length() <= 6f)
			{
				_heroHasMoveTarget = false;
			}
			else
			{
				Vector2 dir = toTarget.Normalized();
				hero.Facing = dir;
				float exploreSpeedScale = HasHostilesInRoom() ? 1f : 2f;
				hero.Position = ClampToRoom(hero.Position + dir * hero.Speed * exploreSpeedScale * delta);
			}
		}

		bool hasHostiles = HasHostilesInRoom();
		UpdateRoomResourceOrbs(hero, delta);
		UpdateRoomContainerInteraction(hero, hasHostiles);
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsAlive || !unit.IsPlayerSide || unit.IsHero)
			{
				continue;
			}

			TickRoomUnitState(unit, delta);
			if (unit.HitPauseTime > 0f)
			{
				continue;
			}

			if (unit.KnockbackTime > 0f)
			{
				AdvanceKnockback(unit, delta);
				continue;
			}

			if (unit.SkillMoveTime > 0f)
			{
				AdvanceShieldRush(unit, delta);
				continue;
			}

			if (unit.StaggerTime > 0f || unit.AttackWindupTime > 0f || unit.RecoveryTime > 0f)
			{
				continue;
			}

			RoomUnit target = FindNearestTarget(unit, false);
			if (hasHostiles && target != null)
			{
				StepUnitCombat(unit, target, delta);
			}
			else
			{
				Vector2 follow = GetAllyFollowAnchor(hero, unit);
				Vector2 dir = follow - unit.Position;
				if (dir.Length() > 14f)
				{
					unit.Facing = dir.Normalized();
					unit.Position = ClampToRoom(unit.Position + unit.Facing * unit.Speed * 0.86f * delta);
				}
			}
		}

		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsAlive || unit.IsPlayerSide)
			{
				continue;
			}

			TickRoomUnitState(unit, delta);
			if (unit.HitPauseTime > 0f)
			{
				continue;
			}

			if (unit.KnockbackTime > 0f)
			{
				AdvanceKnockback(unit, delta);
				continue;
			}

			if (unit.SkillMoveTime > 0f)
			{
				AdvanceShieldRush(unit, delta);
				continue;
			}

			if (unit.StaggerTime > 0f || unit.AttackWindupTime > 0f || unit.RecoveryTime > 0f)
			{
				continue;
			}

			RoomUnit target = FindNearestTarget(unit, true);
			if (target != null)
			{
				StepUnitCombat(unit, target, delta);
			}
		}

		if (hero.IsAlive && HasHostilesInRoom())
		{
			RoomUnit heroTarget = FindNearestTarget(hero, false);
			if (heroTarget != null && !_heroHasMoveTarget)
			{
				StepUnitCombat(hero, heroTarget, delta);
			}
		}

		ResolveRoomUnitCollisions(hero);

		if (_pendingExitNodeId >= 0)
		{
			Rect2 door = GetRoomExitRect(_nodes[_playerNodeId], _pendingExitNodeId).Grow(8f);
			if (door.HasPoint(hero.Position))
			{
				Vector2 entryDirection = GetOppositeDirection(_pendingExitDirection);
				int nextNodeId = _pendingExitNodeId;
				_pendingExitNodeId = -1;
				EnterNodeRoom(nextNodeId, entryDirection, true);
			}
		}

		if (!HasLivingPlayerRoomUnits())
		{
			_playerHp = 0;
			_runBackpack.Clear();
			_overflowBackpackItems.Clear();
			_hasDraggedBackpackItem = false;
			_draggedBackpackItem = null;
			_runEnded = true;
			_runFailed = true;
			_selectedContainerIndex = -1;
			_showSearchConfirm = false;
			_status = "全队失去作战能力。";
			return;
		}

		if (hero.Hp <= 0)
		{
			bool allyAlive = false;
			for (int i = 0; i < _roomUnits.Count; i++)
			{
				if (_roomUnits[i].IsPlayerSide && !_roomUnits[i].IsHero && _roomUnits[i].IsAlive)
				{
					allyAlive = true;
					break;
				}
			}

			if (allyAlive)
			{
				hero.Hp = 1;
				_playerHp = 1;
			}
		}
	}

	private bool HasLivingPlayerRoomUnits()
	{
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			if (_roomUnits[i].IsPlayerSide && _roomUnits[i].IsAlive)
			{
				return true;
			}
		}

		return false;
	}

	private Vector2 GetAllyFollowAnchor(RoomUnit hero, RoomUnit unit)
	{
		int followIndex = 0;
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit candidate = _roomUnits[i];
			if (!candidate.IsPlayerSide || candidate.IsHero || !candidate.IsAlive)
			{
				continue;
			}

			if (candidate == unit)
			{
				break;
			}

			followIndex++;
		}

		Vector2[] slots =
		[
			new Vector2(-32f, -34f),
			new Vector2(-58f, 0f),
			new Vector2(-32f, 34f),
			new Vector2(-84f, -24f),
			new Vector2(-84f, 24f),
			new Vector2(-110f, 0f),
		];

		Vector2 offset;
		if (followIndex < slots.Length)
		{
			offset = slots[followIndex];
		}
		else
		{
			int extra = followIndex - slots.Length;
			int row = extra / 3;
			int col = extra % 3;
			offset = new Vector2(-132f - row * 22f, (col - 1) * 28f);
		}

		return ClampToRoom(hero.Position + offset);
	}

	private void ResolveRoomUnitCollisions(RoomUnit hero)
	{
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit a = _roomUnits[i];
			if (!a.IsAlive)
			{
				continue;
			}

			for (int j = i + 1; j < _roomUnits.Count; j++)
			{
				RoomUnit b = _roomUnits[j];
				if (!b.IsAlive)
				{
					continue;
				}

				float minDistance = GetRoomUnitCollisionRadius(a) + GetRoomUnitCollisionRadius(b);
				Vector2 delta = b.Position - a.Position;
				float distance = delta.Length();
				if (distance >= minDistance || minDistance <= 0f)
				{
					continue;
				}

				Vector2 separationDir;
				if (distance <= 0.001f)
				{
					int seed = a.Name.Length + b.Name.Length + i + j;
					separationDir = seed % 2 == 0 ? Vector2.Right : Vector2.Left;
				}
				else
				{
					separationDir = delta / distance;
				}

				float overlap = minDistance - Mathf.Max(distance, 0.001f);
				float pushA = 0.5f;
				float pushB = 0.5f;

				if (a == hero && b.IsPlayerSide)
				{
					pushA = 0.12f;
					pushB = 0.88f;
				}
				else if (b == hero && a.IsPlayerSide)
				{
					pushA = 0.88f;
					pushB = 0.12f;
				}
				else if (a.IsPlayerSide == b.IsPlayerSide)
				{
					pushA = 0.35f;
					pushB = 0.65f;
				}

				Vector2 offset = separationDir * overlap;
				a.Position = ClampToRoom(a.Position - offset * pushA);
				b.Position = ClampToRoom(b.Position + offset * pushB);
			}
		}
	}

	private void UpdateRoomContainerInteraction(RoomUnit hero, bool hasHostiles)
	{
		if (hero == null)
		{
			return;
		}

		if (hasHostiles)
		{
			CloseSelectedContainer(true);
			return;
		}

		MapNode node = _nodes[_playerNodeId];
		if (IsHeroInSelectedContainerRange(hero, node))
		{
			return;
		}

		if (_selectedContainerIndex >= 0)
		{
			CloseSelectedContainer(true);
		}

		int bestIndex = -1;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < node.Containers.Count; i++)
		{
			LootContainer container = node.Containers[i];
			if (container.IsEmpty)
			{
				continue;
			}

			float distance = hero.Position.DistanceTo(container.Position);
			if (distance > container.AutoOpenRange || distance >= bestDistance)
			{
				continue;
			}

			bestDistance = distance;
			bestIndex = i;
		}

		if (bestIndex < 0)
		{
			return;
		}

		if (_selectedContainerIndex != bestIndex)
		{
			OpenContainer(bestIndex);
		}
	}

	private bool IsHeroInSelectedContainerRange(RoomUnit hero, MapNode node)
	{
		if (_selectedContainerIndex < 0 || _selectedContainerIndex >= node.Containers.Count)
		{
			return false;
		}

		LootContainer container = node.Containers[_selectedContainerIndex];
		if (container.IsEmpty)
		{
			return false;
		}

		return hero.Position.DistanceTo(container.Position) <= container.AutoOpenRange;
	}

	private void CloseSelectedContainer(bool interruptSearch)
	{
		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex >= 0 && _selectedContainerIndex < node.Containers.Count && interruptSearch)
		{
			LootContainer container = node.Containers[_selectedContainerIndex];
			if (container.ActiveSearchItemIndex >= 0 && container.ActiveSearchItemIndex < container.GridItems.Count)
			{
				GridLootItem item = container.GridItems[container.ActiveSearchItemIndex];
				item.SearchProgress = 0f;
			}

			container.ActiveSearchItemIndex = -1;
		}

		_selectedContainerIndex = -1;
		_showSearchConfirm = false;
	}

	private float GetRoomUnitCollisionRadius(RoomUnit unit)
	{
		if (!unit.IsAlive)
		{
			return 0f;
		}

		if (unit.IsHero)
		{
			return unit.IsRanged ? 13.2f : 14.6f;
		}

		if (unit.IsElite)
		{
			return unit.IsRanged ? 11f : 12.2f;
		}

		return unit.IsRanged ? 8.1f : 9.1f;
	}

	private void StepUnitCombat(RoomUnit attacker, RoomUnit target, float delta)
	{
		Vector2 toTarget = target.Position - attacker.Position;
		float distance = toTarget.Length();
		if (distance <= 0.001f)
		{
			int separationSeed = attacker.Name.Length + target.Name.Length + attacker.MaxHp;
			Vector2 fallbackDir = separationSeed % 2 == 0 ? Vector2.Right : Vector2.Left;
			attacker.Position = ClampToRoom(attacker.Position - fallbackDir * 2f);
			distance = 2f;
			toTarget = target.Position - attacker.Position;
		}

		Vector2 dir = toTarget / distance;
		attacker.Facing = dir;
		if (!attacker.IsRanged)
		{
			StepMeleeUnitCombat(attacker, target, dir, distance, delta);
			return;
		}

		StepRangedUnitCombat(attacker, target, dir, distance, delta);
	}

	private void TickRoomUnitState(RoomUnit unit, float delta)
	{
		if (unit.HitPauseTime > 0f)
		{
			unit.HitPauseTime = Mathf.Max(0f, unit.HitPauseTime - delta);
			unit.HitFlash = Mathf.Max(0f, unit.HitFlash - delta);
			return;
		}

		unit.AttackCooldown = Mathf.Max(0f, unit.AttackCooldown - delta);
		unit.ActiveSkillCooldown = Mathf.Max(0f, unit.ActiveSkillCooldown - delta);
		unit.HitFlash = Mathf.Max(0f, unit.HitFlash - delta);
		unit.StaggerTime = Mathf.Max(0f, unit.StaggerTime - delta);
		unit.RecoveryTime = Mathf.Max(0f, unit.RecoveryTime - delta);
		unit.KnockbackTime = Mathf.Max(0f, unit.KnockbackTime - delta);
		unit.ShieldRushContactLock = Mathf.Max(0f, unit.ShieldRushContactLock - delta);
		unit.CombatStateTimer = Mathf.Max(0f, unit.CombatStateTimer - delta);
		if (unit.MaxStamina > 0f)
		{
			float regenRate = unit.IsSprinting ? 10f : 18f;
			if (unit.AttackWindupTime > 0f || unit.RecoveryTime > 0f || unit.StaggerTime > 0f)
			{
				regenRate *= 0.45f;
			}

			unit.Stamina = Mathf.Clamp(unit.Stamina + regenRate * delta, 0f, unit.MaxStamina);
		}

		unit.IsSprinting = false;

		if (unit.AttackWindupTime <= 0f)
		{
			return;
		}

		float previousWindup = unit.AttackWindupTime;
		unit.AttackWindupTime = Mathf.Max(0f, unit.AttackWindupTime - delta);
		if (previousWindup > 0f && unit.AttackWindupTime <= 0f)
		{
			ResolvePendingAttack(unit);
		}
	}

	private void StepMeleeUnitCombat(RoomUnit attacker, RoomUnit target, Vector2 dir, float distance, float delta)
	{
		float baseCooldown = Mathf.Max(0.01f, (attacker.IsHero || attacker.IsElite ? 0.54f : 0.46f) * attacker.AttackCycleScale);
		float cooldownRatio = Mathf.Clamp(attacker.AttackCooldown / baseCooldown, 0f, 1f);
		int flankSeed = attacker.Name.Length + attacker.MaxHp + (attacker.IsPlayerSide ? 1 : 0);
		Vector2 side = new Vector2(-dir.Y, dir.X) * (flankSeed % 2 == 0 ? -1f : 1f);
		float attackBand = attacker.AttackRange + 2f;
		float regroupBand = attacker.AttackRange + 12f;
		float retreatBand = attacker.AttackRange + 24f + cooldownRatio * 10f;
		bool wantsSprint = distance > attacker.AttackRange + 34f
			&& attacker.AttackCooldown <= baseCooldown * 0.35f
			&& attacker.Stamina > attacker.MaxStamina * 0.12f;
		float chaseSpeed = GetRoomMoveSpeed(attacker, wantsSprint, delta, 1f);
		float controlSpeed = GetRoomMoveSpeed(attacker, false, delta, 1f);

		if (attacker.AttackWindupTime > 0f || attacker.RecoveryTime > 0f)
		{
			attacker.CombatState = RoomCombatState.AttackCommit;
			MoveUnitToward(attacker, target.Position - dir * attackBand, controlSpeed * 0.42f, delta);
			return;
		}

		if (TryStartShieldRush(attacker, target, dir, distance))
		{
			return;
		}

		if (TryStartPikeThrust(attacker, target, dir, distance))
		{
			return;
		}

		if (TryStartBladeRush(attacker, target, dir, distance))
		{
			return;
		}

		if (CanStartAttack(attacker) && distance <= attacker.AttackRange + 4f)
		{
			BeginRoomAttack(attacker, target, 14f);
			return;
		}

		if (distance < Mathf.Max(10f, attacker.AttackRange * 0.55f))
		{
			Vector2 separateTarget = attacker.Position - dir * Ui(18f) + side * Ui(10f);
			attacker.CombatState = RoomCombatState.Retreat;
			MoveUnitToward(attacker, separateTarget, controlSpeed * 0.95f, delta);
			return;
		}

		if (attacker.AttackCooldown > baseCooldown * 0.35f)
		{
			attacker.CombatState = RoomCombatState.Retreat;
			Vector2 retreatTarget = target.Position - dir * retreatBand + side * Ui(8f);
			MoveUnitToward(attacker, retreatTarget, controlSpeed * 0.92f, delta);
			return;
		}

		if (distance > attackBand)
		{
			attacker.CombatState = RoomCombatState.Advance;
			Vector2 engageTarget = target.Position - dir * attackBand + side * Ui(4f);
			MoveUnitToward(attacker, engageTarget, chaseSpeed, delta);
			return;
		}

		attacker.CombatState = RoomCombatState.Regroup;
		Vector2 regroupTarget = target.Position - dir * regroupBand + side * Ui(6f);
		MoveUnitToward(attacker, regroupTarget, controlSpeed * 0.6f, delta);
	}

	private bool TryStartShieldRush(RoomUnit attacker, RoomUnit target, Vector2 dir, float distance)
	{
		if (attacker.ActiveSkill != SoldierActiveSkill.ShieldRush
			|| attacker.ActiveSkillCooldown > 0f
			|| attacker.MaxStamina <= 0f
			|| attacker.Stamina < 24f
			|| distance < attacker.AttackRange + 18f
			|| distance > 150f
			|| !CanStartAttack(attacker))
		{
			return false;
		}

		attacker.Stamina = Mathf.Max(0f, attacker.Stamina - 24f);
		attacker.ActiveSkillCooldown = 5f;
		attacker.SkillMoveTime = 0.28f;
		attacker.SkillMoveSpeed = 520f;
		attacker.SkillMoveDirection = dir;
		attacker.PendingAttackTarget = target;
		attacker.CombatState = RoomCombatState.Advance;
		attacker.CombatStateTimer = attacker.SkillMoveTime;
		return true;
	}

	private bool TryStartPikeThrust(RoomUnit attacker, RoomUnit target, Vector2 dir, float distance)
	{
		if (attacker.ActiveSkill != SoldierActiveSkill.PikeThrust
			|| attacker.ActiveSkillCooldown > 0f
			|| attacker.MaxStamina <= 0f
			|| attacker.Stamina < 18f
			|| distance < attacker.AttackRange * 0.72f
			|| distance > attacker.AttackRange + 26f
			|| !CanStartAttack(attacker))
		{
			return false;
		}

		attacker.Stamina = Mathf.Max(0f, attacker.Stamina - 18f);
		attacker.ActiveSkillCooldown = 5f;
		attacker.Facing = dir;
		BeginRoomAttack(attacker, target, attacker.SoldierClass == SoldierClass.VanguardPike ? 28f : 20f, SoldierActiveSkill.PikeThrust);
		return true;
	}

	private bool TryStartBladeRush(RoomUnit attacker, RoomUnit target, Vector2 dir, float distance)
	{
		if (attacker.ActiveSkill != SoldierActiveSkill.BladeRush
			|| attacker.ActiveSkillCooldown > 0f
			|| attacker.MaxStamina <= 0f
			|| attacker.Stamina < 18f
			|| distance < attacker.AttackRange + 8f
			|| distance > 96f
			|| !CanStartAttack(attacker))
		{
			return false;
		}

		attacker.Stamina = Mathf.Max(0f, attacker.Stamina - 18f);
		attacker.ActiveSkillCooldown = 4.5f;
		attacker.Facing = dir;
		BeginRoomAttack(attacker, target, HasEnhancedBladeRush(attacker.SoldierClass) ? 24f : 18f, SoldierActiveSkill.BladeRush);
		return true;
	}

	private bool TryStartSplitArrow(RoomUnit attacker, RoomUnit target, float distance)
	{
		if (attacker.ActiveSkill != SoldierActiveSkill.SplitArrow
			|| attacker.ActiveSkillCooldown > 0f
			|| attacker.MaxStamina <= 0f
			|| attacker.Stamina < 16f
			|| distance < attacker.AttackRange * 0.46f
			|| distance > attacker.AttackRange + 18f
			|| !CanStartAttack(attacker))
		{
			return false;
		}

		attacker.Stamina = Mathf.Max(0f, attacker.Stamina - 16f);
		attacker.ActiveSkillCooldown = 5f;
		BeginRoomAttack(attacker, target, 36f, SoldierActiveSkill.SplitArrow);
		return true;
	}

	private void AdvanceShieldRush(RoomUnit attacker, float delta)
	{
		Vector2 dashDir = attacker.SkillMoveDirection == Vector2.Zero ? attacker.Facing : attacker.SkillMoveDirection;
		dashDir = dashDir == Vector2.Zero ? (attacker.IsPlayerSide ? Vector2.Right : Vector2.Left) : dashDir.Normalized();
		attacker.Facing = dashDir;
		attacker.Position = ClampToRoom(attacker.Position + dashDir * attacker.SkillMoveSpeed * delta);
		ApplyShieldRushHits(attacker, false);
		attacker.SkillMoveTime = Mathf.Max(0f, attacker.SkillMoveTime - delta);
		if (attacker.SkillMoveTime <= 0f)
		{
			ApplyShieldRushHits(attacker, true);
			if (attacker.SoldierClass == SoldierClass.ShieldPlusTwo)
			{
				SpawnShieldRushShockwave(attacker);
			}
		}
	}

	private void ApplyShieldRushHits(RoomUnit attacker, bool impact)
	{
		bool enhancedRush = attacker.SoldierClass == SoldierClass.ShieldPlusTwo;
		float radius = impact ? (enhancedRush ? 34f : 24f) : (enhancedRush ? 22f : 16f);
		int damage = impact
			? Mathf.Max(1, enhancedRush ? attacker.DamageMax + 2 : attacker.DamageMax)
			: Mathf.Max(1, enhancedRush ? attacker.DamageMin + 1 : attacker.DamageMin);
		float stagger = impact ? (enhancedRush ? 0.95f : 0.6f) : (enhancedRush ? 0.45f : 0.3f);
		float hitPause = impact ? (enhancedRush ? 0.11f : 0.08f) : (enhancedRush ? 0.05f : 0.04f);
		float force = impact ? (enhancedRush ? 1580f : 1220f) : (enhancedRush ? 780f : 610f);
		float duration = impact ? (enhancedRush ? 1.34f : 1.08f) : (enhancedRush ? 0.72f : 0.54f);
		bool rangedHit = false;

		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit target = _roomUnits[i];
			if (!target.IsAlive || target == attacker)
			{
				continue;
			}

			float maxDistance = radius + GetRoomUnitCollisionRadius(target);
			if (attacker.Position.DistanceSquaredTo(target.Position) > maxDistance * maxDistance)
			{
				continue;
			}

			if (!impact && target.ShieldRushContactLock > 0f)
			{
				continue;
			}

			target.ShieldRushContactLock = impact ? 0.32f : 0.2f;
			Vector2 hitDir = (target.Position - attacker.Position).Normalized();
			if (hitDir == Vector2.Zero)
			{
				hitDir = attacker.Facing == Vector2.Zero ? (attacker.IsPlayerSide ? Vector2.Right : Vector2.Left) : attacker.Facing.Normalized();
			}

			if (target.IsPlayerSide == attacker.IsPlayerSide)
			{
				target.HitFlash = Mathf.Max(target.HitFlash, 0.16f);
				target.StaggerTime = Mathf.Max(target.StaggerTime, stagger * 0.7f);
				target.KnockbackVelocity = hitDir * (force * 0.72f);
				target.KnockbackTime = Mathf.Max(target.KnockbackTime, duration * 0.72f);
				continue;
			}

			ApplyDirectHit(target, damage, hitDir, stagger, hitPause, force, duration, rangedHit);
		}
	}

	private void SpawnShieldRushShockwave(RoomUnit attacker)
	{
		Vector2 dir = attacker.Facing == Vector2.Zero ? (attacker.IsPlayerSide ? Vector2.Right : Vector2.Left) : attacker.Facing.Normalized();
		_roomShockwaveEffects.Add(new RoomShockwaveEffect
		{
			Origin = attacker.Position,
			Direction = dir,
			Length = 72f,
			Radius = 8f,
			MaxRadius = 84f,
			TimeLeft = 0.28f,
			Duration = 0.28f,
			PlayerSide = attacker.IsPlayerSide,
		});
	}

	private void StepRangedUnitCombat(RoomUnit attacker, RoomUnit target, Vector2 dir, float distance, float delta)
	{
		float baseCooldown = Mathf.Max(0.01f, 0.42f * attacker.AttackCycleScale);
		int flankSeed = attacker.Name.Length + attacker.MaxHp + (attacker.IsPlayerSide ? 1 : 0);
		Vector2 side = new Vector2(-dir.Y, dir.X) * (flankSeed % 2 == 0 ? 1f : -1f);
		float desiredMin = attacker.AttackRange * 0.62f;
		float desiredMax = attacker.AttackRange * 0.9f;
		bool threatened = distance < desiredMin;
		Vector2 retreatTarget = target.Position - dir * (desiredMax + Ui(18f)) + side * Ui(14f);

		if (attacker.AttackWindupTime > 0f || attacker.RecoveryTime > 0f)
		{
			attacker.CombatState = threatened ? RoomCombatState.Retreat : RoomCombatState.AttackCommit;
			Vector2 holdTarget = threatened
				? retreatTarget
				: target.Position - dir * Mathf.Lerp(desiredMin, desiredMax, 0.55f);
			MoveUnitToward(attacker, holdTarget, attacker.Speed * (threatened ? 0.58f : 0.28f), delta);
			return;
		}

		if (threatened)
		{
			if (distance <= attacker.AttackRange + Ui(10f) && CanStartAttack(attacker))
			{
				if (!TryStartSplitArrow(attacker, target, distance))
				{
					BeginRoomAttack(attacker, target, 28f);
				}
			}

			attacker.CombatState = RoomCombatState.Retreat;
			MoveUnitToward(attacker, retreatTarget, attacker.Speed * 0.96f, delta);
			return;
		}

		if (distance > desiredMax)
		{
			attacker.CombatState = RoomCombatState.Advance;
			Vector2 advanceTarget = target.Position - dir * (desiredMax - Ui(8f));
			MoveUnitToward(attacker, advanceTarget, attacker.Speed * 0.82f, delta);
			return;
		}

		if (CanStartAttack(attacker))
		{
			if (!TryStartSplitArrow(attacker, target, distance))
			{
				BeginRoomAttack(attacker, target, 28f);
			}
			return;
		}

		attacker.CombatState = RoomCombatState.Regroup;
		Vector2 strafeTarget = attacker.Position + side * Ui(18f);
		if (attacker.CombatStateTimer <= 0f || attacker.TacticalAnchor == Vector2.Zero)
		{
			attacker.CombatStateTimer = 0.18f + _rng.RandfRange(0f, 0.12f);
			attacker.TacticalAnchor = target.Position - dir * Mathf.Lerp(desiredMin, desiredMax, 0.58f) + side * Ui(18f);
		}

		Vector2 anchoredTarget = attacker.TacticalAnchor;
		if (distance < desiredMin + Ui(8f))
		{
			anchoredTarget -= dir * Ui(10f);
		}
		else if (distance > desiredMax - Ui(8f))
		{
			anchoredTarget += dir * Ui(8f);
		}

		MoveUnitToward(attacker, anchoredTarget.Lerp(strafeTarget, 0.3f), attacker.Speed * 0.55f, delta);
		if (attacker.AttackCooldown <= baseCooldown * 0.15f)
		{
			attacker.CombatState = RoomCombatState.Advance;
		}
	}

	private bool CanStartAttack(RoomUnit attacker)
	{
		return attacker.AttackCooldown <= 0f
			&& attacker.AttackWindupTime <= 0f
			&& attacker.RecoveryTime <= 0f
			&& attacker.StaggerTime <= 0f
			&& attacker.HitPauseTime <= 0f;
	}

	private void BeginRoomAttack(RoomUnit attacker, RoomUnit target, float rangeSlack, SoldierActiveSkill skill = SoldierActiveSkill.None)
	{
		attacker.PendingAttackTarget = target;
		attacker.PendingAttackDamage = _rng.RandiRange(attacker.DamageMin, attacker.DamageMax);
		attacker.PendingAttackSkill = skill;
		attacker.PendingAttackHeavy = attacker.IsHero
			|| attacker.IsElite
			|| attacker.SoldierClass == SoldierClass.Cavalry
			|| attacker.SoldierClass == SoldierClass.VanguardPike
			|| attacker.SoldierClass == SoldierClass.VanguardBlade;
		attacker.PendingAttackRangeSlack = rangeSlack;
		attacker.PendingAttackLungeDistance = attacker.SoldierClass == SoldierClass.Cavalry
			? 16f
			: skill == SoldierActiveSkill.PikeThrust
				? 6f
				: skill == SoldierActiveSkill.BladeRush
					? (HasEnhancedBladeRush(attacker.SoldierClass) ? 24f : 18f)
					: skill == SoldierActiveSkill.SplitArrow
						? 0f
					: 0f;
		if (skill == SoldierActiveSkill.PikeThrust)
		{
			attacker.AttackWindupTime = attacker.SoldierClass == SoldierClass.VanguardPike ? 0.13f : 0.11f;
			attacker.RecoveryTime = attacker.SoldierClass == SoldierClass.VanguardPike ? 0.11f : 0.09f;
		}
		else if (skill == SoldierActiveSkill.BladeRush)
		{
			attacker.AttackWindupTime = HasEnhancedBladeRush(attacker.SoldierClass) ? 0.12f : 0.1f;
			attacker.RecoveryTime = HasEnhancedBladeRush(attacker.SoldierClass) ? 0.12f : 0.1f;
		}
		else if (skill == SoldierActiveSkill.SplitArrow)
		{
			attacker.AttackWindupTime = HasEnhancedSplitArrow(attacker.SoldierClass) ? 0.14f : 0.12f;
			attacker.RecoveryTime = HasEnhancedSplitArrow(attacker.SoldierClass) ? 0.12f : 0.1f;
		}
		else
		{
			attacker.AttackWindupTime = attacker.IsRanged ? 0.13f : (attacker.PendingAttackHeavy ? 0.12f : 0.09f);
			attacker.RecoveryTime = attacker.IsRanged ? 0.1f : 0.08f;
		}
		float baseCooldown = skill == SoldierActiveSkill.PikeThrust
			? (attacker.SoldierClass == SoldierClass.VanguardPike ? 0.58f : 0.52f)
			: skill == SoldierActiveSkill.BladeRush
				? (HasEnhancedBladeRush(attacker.SoldierClass) ? 0.54f : 0.48f)
				: skill == SoldierActiveSkill.SplitArrow
					? (HasEnhancedSplitArrow(attacker.SoldierClass) ? 0.6f : 0.52f)
			: attacker.IsRanged ? 0.42f : (attacker.PendingAttackHeavy ? 0.54f : 0.46f);
		attacker.AttackCooldown = baseCooldown * Mathf.Max(0.1f, attacker.AttackCycleScale);
		attacker.CombatState = RoomCombatState.AttackCommit;
		attacker.CombatStateTimer = attacker.AttackWindupTime + attacker.RecoveryTime;
		attacker.TacticalAnchor = attacker.Position;
	}

	private float GetRoomMoveSpeed(RoomUnit unit, bool sprint, float delta, float scale)
	{
		float speed = unit.Speed * scale;
		if (!sprint || !unit.CanSprint || unit.MaxStamina <= 0f || unit.Stamina <= 0f)
		{
			return speed;
		}

		float staminaUse = (unit.IsHero ? 30f : 26f) * delta;
		if (unit.Stamina < staminaUse)
		{
			return speed;
		}

		unit.Stamina = Mathf.Max(0f, unit.Stamina - staminaUse);
		unit.IsSprinting = true;
		return speed * (unit.IsHero ? 1.82f : 1.72f);
	}

	private void MoveUnitToward(RoomUnit unit, Vector2 target, float speed, float delta)
	{
		Vector2 move = target - unit.Position;
		if (move.LengthSquared() <= 1f)
		{
			return;
		}

		unit.Position = ClampToRoom(unit.Position + move.Normalized() * speed * delta);
	}

	private void AdvanceKnockback(RoomUnit unit, float delta)
	{
		unit.Position = ClampToRoom(unit.Position + unit.KnockbackVelocity * delta);
		unit.KnockbackVelocity *= 0.92f;
	}

	private void ResolvePendingAttack(RoomUnit attacker)
	{
		RoomUnit target = attacker.PendingAttackTarget;
		int damage = attacker.PendingAttackDamage;
		float rangeSlack = attacker.PendingAttackRangeSlack;
		bool heavy = attacker.PendingAttackHeavy;
		SoldierActiveSkill pendingSkill = attacker.PendingAttackSkill;
		attacker.PendingAttackTarget = null;
		attacker.PendingAttackDamage = 0;
		attacker.PendingAttackRangeSlack = 0f;
		attacker.PendingAttackLungeDistance = 0f;
		attacker.PendingAttackHeavy = false;
		attacker.PendingAttackSkill = SoldierActiveSkill.None;

		if (!attacker.IsAlive || target == null || !target.IsAlive)
		{
			return;
		}

		Vector2 toTarget = target.Position - attacker.Position;
		float distance = toTarget.Length();
		float maxDistance = attacker.AttackRange + rangeSlack;
		if (distance > maxDistance)
		{
			return;
		}

		Vector2 dir = distance > 0.001f ? toTarget / distance : (attacker.IsPlayerSide ? Vector2.Right : Vector2.Left);
		attacker.Facing = dir;

		if (pendingSkill == SoldierActiveSkill.PikeThrust)
		{
			ResolvePikeThrust(attacker, target, dir, damage, heavy);
			return;
		}

		if (pendingSkill == SoldierActiveSkill.BladeRush)
		{
			ResolveBladeRush(attacker, target, dir, damage, heavy);
			return;
		}

		if (attacker.IsRanged)
		{
			ApplyArcherCritBonus(attacker, target, ref damage, ref heavy);
			PlayAttackSfx(true, heavy);
			if (pendingSkill == SoldierActiveSkill.SplitArrow)
			{
				ResolveSplitArrow(attacker, target, damage, heavy);
			}
			else
			{
				SpawnRoomProjectileEffect(attacker.Position + dir * 10f, target, damage, attacker.IsPlayerSide, heavy);
			}
		}
		else
		{
			PlayAttackSfx(false, heavy);
			if (attacker.PassiveSkill == SoldierPassiveSkill.Brace && distance >= attacker.AttackRange * 0.72f)
			{
				damage += HasStrengthenedPikePassive(attacker.SoldierClass) ? 2 : 1;
				heavy = true;
			}
			else if (attacker.PassiveSkill == SoldierPassiveSkill.Executioner)
			{
				ApplyBladePassiveBonus(attacker, target, ref damage, ref heavy);
			}

			ApplyProjectileOrSlashHit(target, damage, dir, heavy, false);
			SpawnRoomMeleeArcEffect(attacker, attacker.IsPlayerSide, heavy);
		}
	}

	private void ResolvePikeThrust(RoomUnit attacker, RoomUnit target, Vector2 dir, int damage, bool heavy)
	{
		bool enhanced = HasEnhancedPikeThrust(attacker.SoldierClass);
		float length = enhanced ? 82f : 64f;
		float halfWidth = enhanced ? 18f : 10f;
		float force = enhanced ? 1280f : 920f;
		float duration = enhanced ? 0.9f : 0.72f;
		float stagger = enhanced ? 0.42f : 0.28f;
		int bonusDamage = enhanced ? 2 : 1;
		Vector2 origin = attacker.Position + dir * 10f;
		Vector2 side = new(-dir.Y, dir.X);
		bool hitAny = false;
		float nearestAlong = float.MaxValue;
		RoomUnit nearestTarget = null;

		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit candidate = _roomUnits[i];
			if (!candidate.IsAlive || candidate.IsPlayerSide == attacker.IsPlayerSide)
			{
				continue;
			}

			Vector2 toCandidate = candidate.Position - origin;
			float along = toCandidate.Dot(dir);
			if (along < -4f || along > length)
			{
				continue;
			}

			float lateral = Mathf.Abs(toCandidate.Dot(side));
			float allowance = halfWidth + GetRoomUnitCollisionRadius(candidate);
			if (lateral > allowance)
			{
				continue;
			}

			if (!enhanced)
			{
				if (along < nearestAlong)
				{
					nearestAlong = along;
					nearestTarget = candidate;
				}
				continue;
			}

			ApplyDirectHit(candidate, damage + bonusDamage, dir, stagger, heavy ? 0.08f : 0.06f, force, duration, false);
			hitAny = true;
		}

		if (!enhanced && nearestTarget != null)
		{
			ApplyDirectHit(nearestTarget, damage + bonusDamage, dir, stagger, heavy ? 0.08f : 0.06f, force, duration, false);
			hitAny = true;
		}

		if (!hitAny)
		{
			return;
		}

		PlayAttackSfx(false, true);
		SpawnPikeThrustShockwave(attacker, enhanced);
	}

	private void ResolveBladeRush(RoomUnit attacker, RoomUnit target, Vector2 dir, int damage, bool heavy)
	{
		bool enhanced = HasEnhancedBladeRush(attacker.SoldierClass);
		if (attacker.PendingAttackLungeDistance > 0f)
		{
			attacker.Position = ClampToRoom(attacker.Position + dir * attacker.PendingAttackLungeDistance);
		}

		ApplyBladePassiveBonus(attacker, target, ref damage, ref heavy);
		int strikeDamage = damage + (enhanced ? 3 : 2);
		float stagger = enhanced ? 0.4f : 0.28f;
		float force = enhanced ? 1280f : 960f;
		float duration = enhanced ? 0.92f : 0.7f;
		ApplyDirectHit(target, strikeDamage, dir, stagger, heavy ? 0.08f : 0.06f, force, duration, false);

		if (enhanced)
		{
			for (int i = 0; i < _roomUnits.Count; i++)
			{
				RoomUnit candidate = _roomUnits[i];
				if (!candidate.IsAlive || candidate == target || candidate.IsPlayerSide == attacker.IsPlayerSide)
				{
					continue;
				}

				float radius = 24f + GetRoomUnitCollisionRadius(candidate);
				if (candidate.Position.DistanceSquaredTo(target.Position) > radius * radius)
				{
					continue;
				}

				ApplyDirectHit(candidate, Mathf.Max(2, strikeDamage - 3), dir, 0.22f, 0.05f, 720f, 0.48f, false);
			}

			_roomShockwaveEffects.Add(new RoomShockwaveEffect
			{
				Origin = attacker.Position,
				Direction = dir,
				Length = 44f,
				Radius = 5f,
				MaxRadius = 18f,
				TimeLeft = 0.18f,
				Duration = 0.18f,
				PlayerSide = attacker.IsPlayerSide,
			});
		}

		PlayAttackSfx(false, true);
		SpawnRoomMeleeArcEffect(attacker, attacker.IsPlayerSide, true);
	}

	private void ApplyBladePassiveBonus(RoomUnit attacker, RoomUnit target, ref int damage, ref bool heavy)
	{
		if (attacker.PassiveSkill != SoldierPassiveSkill.Executioner || target == null || !target.IsAlive)
		{
			return;
		}

		bool wounded = target.MaxHp > 0 && target.Hp <= Mathf.CeilToInt(target.MaxHp * 0.5f);
		bool controlled = target.StaggerTime > 0.05f || target.KnockbackTime > 0.05f || target.CombatState == RoomCombatState.Retreat;
		if (!wounded && !controlled)
		{
			return;
		}

		damage += HasStrengthenedBladePassive(attacker.SoldierClass) ? 2 : 1;
		heavy = true;
		if (HasStrengthenedBladePassive(attacker.SoldierClass))
		{
			attacker.AttackCooldown *= 0.72f;
		}
	}

	private void ApplyArcherCritBonus(RoomUnit attacker, RoomUnit target, ref int damage, ref bool heavy)
	{
		if (attacker.PassiveSkill != SoldierPassiveSkill.Deadeye)
		{
			return;
		}

		float critChance = HasStrengthenedArcherPassive(attacker.SoldierClass) ? 0.34f : 0.2f;
		if (_rng.Randf() >= critChance)
		{
			return;
		}

		float critScale = HasStrengthenedArcherPassive(attacker.SoldierClass) ? 2.15f : 1.8f;
		damage = Mathf.Max(damage + 1, Mathf.RoundToInt(damage * critScale));
		heavy = true;
		if (target != null && target.IsAlive)
		{
			target.HitFlash = Mathf.Max(target.HitFlash, 0.14f);
		}
	}

	private void ResolveSplitArrow(RoomUnit attacker, RoomUnit primaryTarget, int damage, bool heavy)
	{
		bool enhanced = HasEnhancedSplitArrow(attacker.SoldierClass);
		int extraShots = enhanced ? 3 : 2;
		float searchRadius = enhanced ? 82f : 58f;
		int splashDamage = Mathf.Max(1, enhanced ? damage - 1 : damage - 2);
		Vector2 origin = attacker.Position + attacker.Facing.Normalized() * 10f;
		SpawnRoomProjectileEffect(origin, primaryTarget, damage, attacker.IsPlayerSide, heavy);

		int spawned = 0;
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit candidate = _roomUnits[i];
			if (!candidate.IsAlive || candidate == primaryTarget || candidate.IsPlayerSide == attacker.IsPlayerSide)
			{
				continue;
			}

			if (candidate.Position.DistanceSquaredTo(primaryTarget.Position) > searchRadius * searchRadius)
			{
				continue;
			}

			SpawnRoomProjectileEffect(origin, candidate, splashDamage, attacker.IsPlayerSide, enhanced);
			spawned++;
			if (spawned >= extraShots)
			{
				break;
			}
		}
	}

	private void SpawnPikeThrustShockwave(RoomUnit attacker, bool enhanced)
	{
		Vector2 dir = attacker.Facing == Vector2.Zero ? (attacker.IsPlayerSide ? Vector2.Right : Vector2.Left) : attacker.Facing.Normalized();
		_roomShockwaveEffects.Add(new RoomShockwaveEffect
		{
			Origin = attacker.Position + dir * 12f,
			Direction = dir,
			Length = enhanced ? 84f : 62f,
			Radius = enhanced ? 5f : 4f,
			MaxRadius = enhanced ? 24f : 14f,
			TimeLeft = enhanced ? 0.24f : 0.18f,
			Duration = enhanced ? 0.24f : 0.18f,
			PlayerSide = attacker.IsPlayerSide,
		});
	}

	private void ApplyRoomHit(RoomUnit attacker, RoomUnit target, int damage, Vector2 dir, bool heavy)
	{
		int finalDamage = GetMitigatedDamage(target, damage, attacker.IsRanged);
		target.Hp = Mathf.Max(0, target.Hp - finalDamage);
		target.HitFlash = heavy ? 0.32f : 0.24f;
		target.StaggerTime = Mathf.Max(target.StaggerTime, attacker.IsRanged ? 0.12f : (heavy ? 0.24f : 0.18f));
		target.HitPauseTime = Mathf.Max(target.HitPauseTime, heavy ? 0.08f : 0.055f);
		attacker.HitPauseTime = Mathf.Max(attacker.HitPauseTime, heavy ? 0.07f : 0.05f);
		ApplyRoomKnockback(target, dir, attacker.IsRanged, heavy);

		if (target.Hp <= 0)
		{
			HandleUnitDeath(target);
		}
	}

	private void SpawnRoomProjectileEffect(Vector2 from, RoomUnit target, int damage, bool playerSide, bool heavy)
	{
		Vector2 to = target != null && target.IsAlive ? target.Position : from + (playerSide ? Vector2.Right : Vector2.Left) * 24f;
		Vector2 dir = (to - from).Normalized();
		if (dir == Vector2.Zero)
		{
			dir = playerSide ? Vector2.Right : Vector2.Left;
		}

		_roomProjectileEffects.Add(new RoomProjectileEffect
		{
			Position = from,
			PreviousPosition = from,
			Velocity = dir * (heavy ? 540f : 460f),
			TimeLeft = heavy ? 0.5f : 0.42f,
			Duration = heavy ? 0.5f : 0.42f,
			PlayerSide = playerSide,
			Heavy = heavy,
			Target = target,
			Damage = damage,
		});
	}

	private void SpawnRoomMeleeArcEffect(RoomUnit owner, bool playerSide, bool heavy)
	{
		_roomMeleeArcEffects.Add(new RoomMeleeArcEffect
		{
			Origin = owner.Position,
			FacingAngle = owner.Facing == Vector2.Zero ? (playerSide ? 0f : Mathf.Pi) : owner.Facing.Angle(),
			Range = heavy ? 32f : 28f,
			ArcHalfAngle = heavy ? 0.72f : 0.6f,
			TimeLeft = heavy ? 0.3f : 0.25f,
			Duration = heavy ? 0.3f : 0.25f,
			PlayerSide = playerSide,
			Heavy = heavy,
			Owner = owner,
		});
	}

	private void UpdateRoomImpactEffects(float delta)
	{
		for (int i = _roomProjectileEffects.Count - 1; i >= 0; i--)
		{
			RoomProjectileEffect effect = _roomProjectileEffects[i];
			effect.PreviousPosition = effect.Position;
			if (effect.Target != null && effect.Target.IsAlive)
			{
				Vector2 toTarget = effect.Target.Position - effect.Position;
				Vector2 dir = toTarget.Normalized();
				if (dir != Vector2.Zero)
				{
					effect.Velocity = dir * effect.Velocity.Length();
				}
			}

			effect.Position = ClampToRoom(effect.Position + effect.Velocity * delta);
			effect.TimeLeft -= delta;
			if (effect.Target != null && effect.Target.IsAlive && effect.Position.DistanceTo(effect.Target.Position) <= 10f)
			{
				Vector2 dir = (effect.Target.Position - effect.PreviousPosition).Normalized();
				if (dir == Vector2.Zero)
				{
					dir = effect.PlayerSide ? Vector2.Right : Vector2.Left;
				}

				ApplyProjectileOrSlashHit(effect.Target, effect.Damage, dir, effect.Heavy, true);
				_roomProjectileEffects.RemoveAt(i);
				continue;
			}

			if (effect.TimeLeft <= 0f)
			{
				_roomProjectileEffects.RemoveAt(i);
			}
		}

		for (int i = _roomMeleeArcEffects.Count - 1; i >= 0; i--)
		{
			RoomMeleeArcEffect effect = _roomMeleeArcEffects[i];
			if (effect.Owner == null || !effect.Owner.IsAlive)
			{
				_roomMeleeArcEffects.RemoveAt(i);
				continue;
			}

			effect.Origin = effect.Owner.Position;
			if (effect.Owner.Facing != Vector2.Zero)
			{
				effect.FacingAngle = effect.Owner.Facing.Angle();
			}
			effect.TimeLeft -= delta;

			if (effect.TimeLeft <= 0f)
			{
				_roomMeleeArcEffects.RemoveAt(i);
			}
		}

		for (int i = _roomShockwaveEffects.Count - 1; i >= 0; i--)
		{
			RoomShockwaveEffect effect = _roomShockwaveEffects[i];
			effect.TimeLeft -= delta;
			float t = 1f - Mathf.Clamp(effect.TimeLeft / Mathf.Max(0.001f, effect.Duration), 0f, 1f);
			effect.Radius = Mathf.Lerp(8f, effect.MaxRadius, t);
			if (effect.TimeLeft <= 0f)
			{
				_roomShockwaveEffects.RemoveAt(i);
			}
		}
	}

	private void ApplyProjectileOrSlashHit(RoomUnit target, int damage, Vector2 dir, bool heavy, bool rangedHit)
	{
		if (target == null || !target.IsAlive)
		{
			return;
		}

		int finalDamage = GetMitigatedDamage(target, damage, rangedHit);
		target.Hp = Mathf.Max(0, target.Hp - finalDamage);
		target.HitFlash = heavy ? 0.32f : 0.24f;
		target.StaggerTime = Mathf.Max(target.StaggerTime, rangedHit ? 0.14f : (heavy ? 0.24f : 0.18f));
		target.HitPauseTime = Mathf.Max(target.HitPauseTime, heavy ? 0.08f : 0.055f);
		ApplyRoomKnockback(target, dir, rangedHit, heavy);
		PlayHitSfx(heavy, target.Hp <= 0);

		if (target.Hp <= 0)
		{
			HandleUnitDeath(target);
		}
	}

	private int GetMitigatedDamage(RoomUnit target, int damage, bool rangedHit)
	{
		if (target.BlockAnyDamageChance > 0f && _rng.Randf() < target.BlockAnyDamageChance)
		{
			target.HitFlash = Mathf.Max(target.HitFlash, 0.18f);
			return 0;
		}

		float scaledDamage = damage;
		if (rangedHit)
		{
			scaledDamage *= target.ProjectileDamageScale;
		}

		scaledDamage -= target.Armor;
		return Mathf.Max(1, Mathf.RoundToInt(scaledDamage));
	}

	private void ApplyDirectHit(RoomUnit target, int damage, Vector2 dashDir, float stagger, float hitPause, float knockbackForce, float knockbackDuration, bool rangedHit)
	{
		if (target == null || !target.IsAlive)
		{
			return;
		}

		int finalDamage = GetMitigatedDamage(target, damage, rangedHit);
		target.Hp = Mathf.Max(0, target.Hp - finalDamage);
		target.HitFlash = 0.28f;
		target.StaggerTime = Mathf.Max(target.StaggerTime, stagger);
		target.HitPauseTime = Mathf.Max(target.HitPauseTime, hitPause);
		if (dashDir != Vector2.Zero)
		{
			target.KnockbackVelocity = dashDir * knockbackForce;
			target.KnockbackTime = Mathf.Max(target.KnockbackTime, knockbackDuration);
		}

		PlayHitSfx(true, target.Hp <= 0);

		if (target.Hp <= 0)
		{
			HandleUnitDeath(target);
		}
	}

	private void ApplyRoomKnockback(RoomUnit target, Vector2 dir, bool rangedHit, bool heavy)
	{
		float knockbackForce;
		float knockbackDuration;
		if (rangedHit)
		{
			knockbackForce = heavy ? 280f : 200f;
			knockbackDuration = heavy ? 0.24f : 0.18f;
		}
		else
		{
			knockbackForce = heavy ? 1080f : 820f;
			knockbackDuration = heavy ? 0.88f : 0.68f;
		}

		target.KnockbackVelocity = dir * knockbackForce;
		target.KnockbackTime = knockbackDuration;
	}

	private void HandleUnitDeath(RoomUnit dead)
	{
		if (!dead.IsHero || !HasLivingPlayerAlliesExcludingHero())
		{
			_nodes[_playerNodeId].Corpses.Add(new RoomCorpseMarker
			{
				IsPlayerSide = dead.IsPlayerSide,
				IsElite = dead.IsElite,
				IsHero = dead.IsHero,
				IsAiSquad = dead.IsAiSquad,
				Position = dead.Position,
			});
		}

		MapNode node = _nodes[_playerNodeId];
		AddDeathDrops(node, dead);

		if (!dead.IsPlayerSide)
		{
			if (!HasHostilesInRoom())
			{
				AiSquad squad = GetSquadAtNode(node.Id);
				if (squad != null)
				{
					squad.Intent = AiIntent.Defeated;
					squad.Strength = 0;
				}

				node.Threat = 0;
				GrantRunSoldierExperience(1, "清空房间");
			}
		}
		else if (!dead.IsHero)
		{
			for (int i = _runSoldiers.Count - 1; i >= 0; i--)
			{
				if (_runSoldiers[i].Name == dead.RosterName)
				{
					_runSoldiers.RemoveAt(i);
					RecalculatePlayerStrength();
					break;
				}
			}
		}

		if (dead.IsPlayerSide && (_runBackpack.Count > 0 || _overflowBackpackItems.Count > 0 || _hasDraggedBackpackItem))
		{
			AutoOrganizeBackpack();
		}
	}

	private void AddDeathDrops(MapNode node, RoomUnit dead)
	{
		if (!dead.IsHero)
		{
			SpawnMoneyOrb(dead);
		}

		if (dead.IsElite)
		{
			LootContainer elite = new()
			{
				Label = dead.Name,
				Kind = ContainerKind.EliteCorpse,
				Position = ClampToRoom(dead.Position),
				Tint = GetRoomUnitBaseColor(dead),
				AutoOpenRange = 60f,
			};
			AddRandomDeathLoot(dead, elite, 3, true);
			PromoteEliteEquipment(elite);
			node.Containers.Add(elite);
			return;
		}

		if (!dead.IsPlayerSide && _rng.Randf() < 0.05f)
		{
			LootContainer special = new()
			{
				Label = dead.IsAiSquad ? $"{dead.Name} drop" : $"{dead.Name} loot",
				Kind = ContainerKind.CorpsePile,
				Position = ClampToRoom(dead.Position),
				Tint = GetRoomUnitBaseColor(dead),
				AutoOpenRange = 58f,
			};
			AddRandomDeathLoot(dead, special, 1, false);
			node.Containers.Add(special);
		}
	}

	private void AddRandomDeathLoot(RoomUnit dead, LootContainer container, int hiddenCount, bool promoteEquipment)
	{
		if (promoteEquipment)
		{
			AddContainerLoot(container, dead.IsAiSquad ? "队长佩刀" : "精英武装", true);
			AddContainerLoot(container, dead.IsAiSquad ? "队长护甲" : "厚皮护甲", true);
		}
		else if (_rng.Randf() < 0.35f)
		{
			AddContainerLoot(container, dead.IsPlayerSide ? "遗落补给" : "战场零件", true);
		}

		for (int i = 0; i < hiddenCount; i++)
		{
			AddContainerLoot(container, RollLootItem(), false);
		}

		if (_rng.Randf() < 0.4f)
		{
			AddContainerLoot(container, dead.IsAiSquad ? "染血徽章" : "破损零件", false);
		}
	}

	private void SpawnMoneyOrb(RoomUnit dead)
	{
		int amount = dead.IsPlayerSide ? _rng.RandiRange(4, 9) : _rng.RandiRange(6, 14);
		if (dead.IsAiSquad)
		{
			amount += 3;
		}

		RoomResourceOrb orb = new()
		{
			Position = ClampToRoom(dead.Position),
			Velocity = new Vector2(_rng.RandfRange(-28f, 28f), _rng.RandfRange(-36f, -8f)),
			MoneyAmount = amount,
			Tint = dead.IsPlayerSide ? new Color(0.56f, 0.95f, 0.72f, 1f) : new Color(1f, 0.84f, 0.34f, 1f),
		};
		_roomResourceOrbs.Add(orb);
	}

	private void UpdateRoomResourceOrbs(RoomUnit hero, float delta)
	{
		for (int i = _roomResourceOrbs.Count - 1; i >= 0; i--)
		{
			RoomResourceOrb orb = _roomResourceOrbs[i];
			Vector2 toHero = hero.Position - orb.Position;
			float distance = toHero.Length();
			if (distance <= 20f)
			{
				CollectResourceOrb(orb);
				_roomResourceOrbs.RemoveAt(i);
				continue;
			}

			if (distance <= 128f)
			{
				Vector2 pullDir = distance > 0.001f ? toHero / distance : Vector2.Zero;
				orb.Velocity = orb.Velocity.Lerp(pullDir * 240f, 0.18f);
			}
			else
			{
				orb.Velocity = orb.Velocity.Lerp(Vector2.Zero, 0.08f);
			}

			orb.Position = ClampToRoom(orb.Position + orb.Velocity * delta);
		}
	}

	private void CollectResourceOrb(RoomResourceOrb orb)
	{
		_money += orb.MoneyAmount;
		_runMoneyLooted += orb.MoneyAmount;
		LogEvent($"获得 {orb.MoneyAmount} 资金。");
	}

	private void AddContainerLoot(LootContainer container, string label, bool revealed)
	{
		if (container.GridItems.Count > 0)
		{
			AddGridItem(container, label, revealed);
			return;
		}

		if (revealed)
		{
			container.VisibleItems.Add(label);
		}
		else
		{
			container.HiddenItems.Add(label);
		}
	}

	private void AddBackpackItemToContainer(LootContainer container, BackpackItem backpackItem)
	{
		GridLootItem item = new()
		{
			Label = backpackItem.Label,
			Rarity = backpackItem.Rarity,
			Size = backpackItem.Size,
			AcquiredInRun = backpackItem.AcquiredInRun,
			Revealed = true,
			SearchTime = GetGridSearchTime(backpackItem.Rarity),
		};

		while (!TryPlaceGridItem(container, item))
		{
			container.GridSize = new Vector2I(container.GridSize.X, container.GridSize.Y + 1);
		}

		container.GridItems.Add(item);
	}

	private bool HasLivingPlayerAlliesExcludingHero()
	{
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (unit.IsPlayerSide && !unit.IsHero && unit.IsAlive)
			{
				return true;
			}
		}

		return false;
	}

	private void RelayoutAlliesAroundHero()
	{
		RoomUnit hero = FindHeroUnit();
		if (hero == null)
		{
			return;
		}

		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsPlayerSide || unit.IsHero || !unit.IsAlive)
			{
				continue;
			}

			unit.Position = GetAllyFollowAnchor(hero, unit);
		}
	}
}
