using System.Collections.Generic;
using Godot;

public partial class RaidMapDemo
{
	private void DrawRoomViewUnified()
	{
		DrawRect(_mapRect, new Color(0.07f, 0.07f, 0.08f), true);
		DrawRect(_mapRect, new Color(0.34f, 0.32f, 0.28f), false, 2f);
		if (_selectedMapTemplate == 1)
		{
			DrawBorderKeepBackdrop();
		}
		else
		{
			DrawMonasteryBackdrop();
		}

		MapNode node = _nodes[_playerNodeId];
		Rect2 arena = GetRoomArenaRect();
		DrawRect(arena, new Color(0.1f, 0.12f, 0.17f, 0.94f), true);
		DrawRect(arena, new Color(0.54f, 0.6f, 0.72f, 0.85f), false, 2f);

		for (float x = arena.Position.X + 32f; x < arena.End.X; x += 32f)
		{
			DrawLine(new Vector2(x, arena.Position.Y), new Vector2(x, arena.End.Y), new Color(0.16f, 0.19f, 0.24f, 0.38f), 1f);
		}
		for (float y = arena.Position.Y + 32f; y < arena.End.Y; y += 32f)
		{
			DrawLine(new Vector2(arena.Position.X, y), new Vector2(arena.End.X, y), new Color(0.16f, 0.19f, 0.24f, 0.38f), 1f);
		}

		DrawString(ThemeDB.FallbackFont, arena.Position + new Vector2(Ui(16f), Ui(26f)), node.Name, HorizontalAlignment.Left, -1f, UiFont(22), Colors.White);
		DrawRect(new Rect2(arena.Position + new Vector2(10f, 30f), new Vector2(270f, 20f)), new Color(0.1f, 0.12f, 0.17f, 0.96f), true);
		DrawString(ThemeDB.FallbackFont, arena.Position + new Vector2(14f, 42f), "战斗 / 搜索 / 过门处于同一实时状态", HorizontalAlignment.Left, -1f, 12, new Color(0.88f, 0.92f, 0.98f));
		DrawString(ThemeDB.FallbackFont, arena.Position + new Vector2(14f, 42f), "战斗/搜索/过门为同一实时状态", HorizontalAlignment.Left, -1f, 12, new Color(0.88f, 0.92f, 0.98f));

		DrawRect(new Rect2(arena.Position + new Vector2(10f, 30f), new Vector2(270f, 20f)), new Color(0.1f, 0.12f, 0.17f, 0.96f), true);
		DrawString(ThemeDB.FallbackFont, arena.Position + new Vector2(14f, 42f), "战斗 / 搜索 / 过门处于同一实时状态", HorizontalAlignment.Left, -1f, 12, new Color(0.88f, 0.92f, 0.98f));

		if (_heroHasMoveTarget)
		{
			RoomUnit hero = FindHeroUnit();
			if (hero != null)
			{
				DrawLine(hero.Position, _heroMoveTarget, new Color(0.56f, 0.9f, 0.98f, 0.72f), 1.8f);
			}
		}

		DrawRoomExitsUnified(node);
		DrawRoomContainers(node);
		DrawRoomCorpses(node);
		DrawRoomResourceOrbs();
		DrawRoomImpactEffects();
		DrawRoomUnits();
	}

	private void DrawRoomExitsUnified(MapNode node)
	{
		for (int i = 0; i < node.Links.Count; i++)
		{
			int linkedNodeId = node.Links[i];
			MapNode linkedNode = _nodes[linkedNodeId];
			Rect2 exitRect = GetRoomExitRect(node, linkedNodeId);
			bool pending = linkedNodeId == _pendingExitNodeId;
			Color fill = pending ? new Color(0.3f, 0.62f, 0.82f, 0.92f) : new Color(0.28f, 0.34f, 0.44f, 0.9f);
			Color border = pending ? new Color(1f, 0.92f, 0.58f, 1f) : new Color(0.85f, 0.92f, 1f, 0.96f);
			DrawRect(exitRect, fill, true);
			DrawRect(exitRect, border, false, 2f);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(Ui(8f), Ui(19f)), GetCleanExitDirectionLabel(GetExitDirection(node, linkedNodeId)), HorizontalAlignment.Left, exitRect.Size.X - Ui(16f), UiFont(13), Colors.White);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(Ui(8f), Ui(38f)), linkedNode.Name, HorizontalAlignment.Left, exitRect.Size.X - Ui(16f), UiFont(12), new Color(0.9f, 0.95f, 1f));
			_buttons.Add(new ButtonDef(exitRect, "use_exit", linkedNodeId));
		}
	}

	private void DrawRoomUnits()
	{
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsAlive)
			{
				continue;
			}

			Color body = GetRoomUnitBaseColor(unit);
			if (unit.HitFlash > 0f)
			{
				float flash = Mathf.Clamp(unit.HitFlash * 5f, 0f, 1f);
				body = body.Lerp(Colors.White, flash);
			}

			DrawRoomUnitFigure(unit, body);
			if (unit.AttackWindupTime > 0f)
			{
				float radius = unit.IsHero ? 14f : (unit.IsElite ? 11f : 9.8f);
				Vector2 dir = unit.Facing == Vector2.Zero ? (unit.IsPlayerSide ? Vector2.Right : Vector2.Left) : unit.Facing.Normalized();
				Vector2 normal = new(-dir.Y, dir.X);
				float totalWindup = unit.IsRanged ? 0.13f : (unit.IsHero || unit.IsElite ? 0.12f : 0.09f);
				float windupRatio = totalWindup > 0f ? 1f - Mathf.Clamp(unit.AttackWindupTime / totalWindup, 0f, 1f) : 1f;
				Vector2 windupTip = unit.Position + dir * (radius + 11f + windupRatio * 4f);
				Vector2 windupBase = unit.Position + dir * (radius + 3f);
				Vector2 windupLeft = windupBase + normal * (3f + windupRatio * 3f);
				Vector2 windupRight = windupBase - normal * (3f + windupRatio * 3f);
				Color windupColor = new(1f, 0.94f, 0.72f, 0.95f);
				DrawLine(windupBase, windupTip, windupColor, 2.4f);
				DrawLine(windupTip, windupLeft, windupColor, 2.2f);
				DrawLine(windupTip, windupRight, windupColor, 2.2f);
			}
			Rect2 hpBg = new(unit.Position + new Vector2(-20f, 18f), new Vector2(40f, 4f));
			DrawRect(hpBg, new Color(0.14f, 0.14f, 0.16f), true);
			float ratio = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
			DrawRect(new Rect2(hpBg.Position, new Vector2(hpBg.Size.X * ratio, hpBg.Size.Y)), GetRoomUnitHealthColor(unit), true);
			DrawRect(hpBg, Colors.White, false, 1f);
			if (!unit.IsRanged && unit.MaxStamina > 0f)
			{
				Rect2 staminaBg = new(unit.Position + new Vector2(-20f, 24f), new Vector2(40f, 3f));
				DrawRect(staminaBg, new Color(0.11f, 0.11f, 0.13f, 0.95f), true);
				float staminaRatio = unit.MaxStamina > 0f ? unit.Stamina / unit.MaxStamina : 0f;
				Color staminaColor = unit.IsSprinting
					? new Color(1f, 0.86f, 0.42f)
					: new Color(0.88f, 0.78f, 0.28f);
				DrawRect(new Rect2(staminaBg.Position, new Vector2(staminaBg.Size.X * staminaRatio, staminaBg.Size.Y)), staminaColor, true);
				DrawRect(staminaBg, new Color(1f, 0.96f, 0.72f, 0.7f), false, 0.8f);
			}
			if (!unit.IsPlayerSide && (unit.IsElite || unit.IsAiSquad))
			{
				DrawString(ThemeDB.FallbackFont, unit.Position + new Vector2(-28f, -20f), unit.Name, HorizontalAlignment.Left, 110f, UiFont(12), Colors.White);
			}
		}
	}

	private void DrawRoomContainers(MapNode node)
	{
		RoomUnit hero = FindHeroUnit();
		for (int i = 0; i < node.Containers.Count; i++)
		{
			LootContainer container = node.Containers[i];
			if (container.IsEmpty)
			{
				continue;
			}

			bool selected = i == _selectedContainerIndex;
			bool inRange = hero != null && hero.Position.DistanceTo(container.Position) <= container.AutoOpenRange;
			Color tint = container.Tint;
			Color fill = new Color(tint.R * 0.78f, tint.G * 0.78f, tint.B * 0.78f, 0.96f);
			Color border = selected ? Colors.White : tint.Lightened(inRange ? 0.25f : 0.08f);
			float radius = container.Kind == ContainerKind.EliteCorpse ? 18f : 13f;
			Rect2 body = new(container.Position - new Vector2(radius, radius * 0.78f), new Vector2(radius * 2f, radius * 1.56f));
			DrawRect(body, fill, true);
			DrawRect(body, border, false, selected ? 2.4f : 1.6f);
			DrawLine(body.Position + new Vector2(0f, body.Size.Y * 0.46f), body.End - new Vector2(0f, body.Size.Y * 0.54f), new Color(0.16f, 0.16f, 0.18f, 0.7f), 1.2f);
			DrawCircle(container.Position + new Vector2(0f, -body.Size.Y * 0.58f), radius * 0.46f, border);
			if (inRange || selected)
			{
				DrawArc(container.Position, container.AutoOpenRange, 0f, Mathf.Tau, 28, new Color(tint.R, tint.G, tint.B, selected ? 0.55f : 0.26f), selected ? 2.6f : 1.4f);
			}

			string caption = container.Kind == ContainerKind.EliteCorpse ? "精英容器" : $"容器 {CountNodeLootSingle(container)}";
			DrawString(ThemeDB.FallbackFont, container.Position + new Vector2(-42f, radius + 18f), caption, HorizontalAlignment.Left, 100f, UiFont(11), new Color(0.94f, 0.94f, 0.96f, selected || inRange ? 1f : 0.76f));
		}
	}

	private void DrawRoomResourceOrbs()
	{
		for (int i = 0; i < _roomResourceOrbs.Count; i++)
		{
			RoomResourceOrb orb = _roomResourceOrbs[i];
			Color glow = new Color(orb.Tint.R, orb.Tint.G, orb.Tint.B, 0.28f);
			DrawCircle(orb.Position, 13f, glow);
			DrawCircle(orb.Position, 8f, orb.Tint);
			DrawCircle(orb.Position + new Vector2(-2f, -2f), 3.2f, new Color(1f, 0.97f, 0.82f, 0.95f));
		}
	}

	private void DrawRoomCorpses(MapNode node)
	{
		for (int i = 0; i < node.Corpses.Count; i++)
		{
			RoomCorpseMarker corpse = node.Corpses[i];
			float corpseSize = corpse.IsHero || corpse.IsElite ? 16f : 8f;
			float lineWidth = corpse.IsHero || corpse.IsElite ? 4.4f : 2.8f;
			Color corpseColor = GetRoomCorpseColor(corpse);
			DrawLine(corpse.Position + new Vector2(-corpseSize, -corpseSize), corpse.Position + new Vector2(corpseSize, corpseSize), corpseColor, lineWidth);
			DrawLine(corpse.Position + new Vector2(-corpseSize, corpseSize), corpse.Position + new Vector2(corpseSize, -corpseSize), corpseColor, lineWidth);
		}
	}

	private Color GetRoomUnitBaseColor(RoomUnit unit)
	{
		if (unit.IsPlayerSide)
		{
			return unit.IsHero ? new Color(0.34f, 0.84f, 1f) : new Color(0.5f, 0.92f, 0.72f);
		}

		if (unit.IsAiSquad)
		{
			return unit.IsElite ? new Color(0.98f, 0.64f, 0.26f) : new Color(0.92f, 0.5f, 0.22f);
		}

		return unit.IsElite ? new Color(0.86f, 0.84f, 0.8f) : new Color(0.72f, 0.74f, 0.78f);
	}

	private Color GetRoomUnitHealthColor(RoomUnit unit)
	{
		if (unit.IsPlayerSide)
		{
			return new Color(0.46f, 0.95f, 0.58f);
		}

		return unit.IsAiSquad
			? new Color(0.96f, 0.66f, 0.24f)
			: new Color(0.8f, 0.82f, 0.86f);
	}

	private Color GetRoomCorpseColor(RoomCorpseMarker corpse)
	{
		if (corpse.IsPlayerSide)
		{
			return corpse.IsHero
				? new Color(0.34f, 0.84f, 1f, 0.66f)
				: new Color(0.5f, 0.92f, 0.72f, 0.62f);
		}

		if (corpse.IsAiSquad)
		{
			return corpse.IsElite
				? new Color(0.98f, 0.64f, 0.26f, 0.72f)
				: new Color(0.92f, 0.5f, 0.22f, 0.66f);
		}

		return corpse.IsElite
			? new Color(0.86f, 0.84f, 0.8f, 0.66f)
			: new Color(0.72f, 0.74f, 0.78f, 0.56f);
	}

	private void DrawRoomUnitFigure(RoomUnit unit, Color bodyColor)
	{
		Vector2 dir = unit.Facing == Vector2.Zero ? (unit.IsPlayerSide ? Vector2.Right : Vector2.Left) : unit.Facing.Normalized();
		Vector2 faceSide = new(Mathf.Sign(dir.X == 0f ? (unit.IsPlayerSide ? 1f : -1f) : dir.X), 0f);
		float animTime = Time.GetTicksMsec() * 0.001f;
		float idleBob = Mathf.Sin(animTime * 3.8f + unit.Position.X * 0.03f) * 0.5f;
		bool isRunning = unit.CombatState is RoomCombatState.Advance or RoomCombatState.Retreat;
		bool isAttacking = unit.AttackWindupTime > 0f || unit.RecoveryTime > 0f || unit.HitPauseTime > 0f;
		float runPhase = animTime * (isRunning ? 11f : 4.5f) + unit.Position.X * 0.04f + unit.Position.Y * 0.018f;
		float runSwing = Mathf.Sin(runPhase);
		float runLift = Mathf.Abs(Mathf.Sin(runPhase)) * (isRunning ? 1.2f : 0.2f);
		float attackPose = GetRoomAttackPose(unit);
		bool armoredShieldClass = unit.SoldierClass is SoldierClass.EliteShield or SoldierClass.ShieldPlusOne or SoldierClass.ShieldPlusTwo;
		bool helmetShieldClass = unit.SoldierClass is SoldierClass.ShieldPlusOne or SoldierClass.ShieldPlusTwo;
		float sizeScale = unit.IsHero ? 1.46f : ((unit.IsElite || armoredShieldClass) ? 1.34f : 1.1f);
		float torsoHalfWidth = (unit.IsRanged ? 4.2f : 5.2f) * sizeScale;
		float torsoHeight = (unit.IsHero ? 13f : 11f) * sizeScale;
		float shoulderWidth = torsoHalfWidth + ((unit.IsElite || armoredShieldClass) ? 1.3f : 0.7f) * sizeScale;

		Vector2 feet = unit.Position + new Vector2(0f, 12f * sizeScale + runLift);
		Vector2 hip = feet + new Vector2(0f, -8f * sizeScale);
		Vector2 chest = hip + new Vector2(0f, -torsoHeight * 0.52f);
		Vector2 neck = chest + new Vector2(0f, -torsoHeight * 0.4f);
		Vector2 headCenter = neck + new Vector2(0f, -4.6f * sizeScale + idleBob);
		Vector2 torsoLean = new(faceSide.X * (isRunning ? runSwing * 1.6f : attackPose * 1.9f), 0f);
		hip += torsoLean * 0.35f;
		chest += torsoLean;
		neck += torsoLean;
		headCenter += torsoLean;

		Color outline = new Color(0.05f, 0.06f, 0.08f, 0.95f);
		Color accent = unit.IsPlayerSide
			? new Color(0.88f, 0.96f, 1f, 0.95f)
			: new Color(1f, 0.88f, 0.84f, 0.95f);

		Vector2 torsoTopLeft = chest + new Vector2(-shoulderWidth, -2.8f * sizeScale);
		Vector2 torsoTopRight = chest + new Vector2(shoulderWidth, -2.8f * sizeScale);
		Vector2 torsoBottomLeft = hip + new Vector2(-torsoHalfWidth, 3.6f * sizeScale);
		Vector2 torsoBottomRight = hip + new Vector2(torsoHalfWidth, 3.6f * sizeScale);
		Vector2[] torso =
		[
			torsoTopLeft,
			torsoTopRight,
			torsoBottomRight,
			torsoBottomLeft,
		];
		DrawColoredPolygon(torso, bodyColor);
		DrawPolyline(new[] { torsoTopLeft, torsoTopRight, torsoBottomRight, torsoBottomLeft, torsoTopLeft }, outline, 1.6f);
		if (armoredShieldClass)
		{
			Vector2[] breastplate =
			[
				chest + new Vector2(-torsoHalfWidth * 0.76f, -1.4f * sizeScale),
				chest + new Vector2(torsoHalfWidth * 0.76f, -1.4f * sizeScale),
				hip + new Vector2(torsoHalfWidth * 0.58f, 2.4f * sizeScale),
				hip + new Vector2(-torsoHalfWidth * 0.58f, 2.4f * sizeScale),
			];
			Color armorColor = bodyColor.Lerp(Colors.White, 0.18f);
			DrawColoredPolygon(breastplate, armorColor);
			DrawPolyline(new[] { breastplate[0], breastplate[1], breastplate[2], breastplate[3], breastplate[0] }, outline, 1.3f);
		}

		float headRadius = (unit.IsHero ? 4.8f : 4.1f) * sizeScale;
		DrawCircle(headCenter, headRadius + 1.2f, outline);
		DrawCircle(headCenter, headRadius, bodyColor.Lerp(Colors.White, unit.IsRanged ? 0.16f : 0.08f));
		if (helmetShieldClass)
		{
			Vector2[] helmet =
			[
				headCenter + new Vector2(-headRadius * 0.92f, -headRadius * 0.35f),
				headCenter + new Vector2(0f, -headRadius * 1.26f),
				headCenter + new Vector2(headRadius * 0.92f, -headRadius * 0.35f),
				headCenter + new Vector2(headRadius * 0.7f, headRadius * 0.25f),
				headCenter + new Vector2(-headRadius * 0.7f, headRadius * 0.25f),
			];
			Color helmetColor = bodyColor.Lerp(Colors.White, 0.22f);
			DrawColoredPolygon(helmet, helmetColor);
			DrawPolyline(new[] { helmet[0], helmet[1], helmet[2], helmet[3], helmet[4], helmet[0] }, outline, 1.2f);
		}
		Vector2 eye = headCenter + new Vector2(faceSide.X * (headRadius * 0.32f), -0.3f);
		DrawCircle(eye, 0.8f, new Color(0.06f, 0.07f, 0.09f, 0.9f));

		Vector2 shoulderFront = chest + new Vector2(faceSide.X * shoulderWidth * 0.72f, -1.6f * sizeScale);
		Vector2 shoulderBack = chest + new Vector2(-faceSide.X * shoulderWidth * 0.52f, -1.1f * sizeScale);
		float frontArmLift = (isAttacking ? -4.8f - attackPose * 2.2f : runSwing * 1.6f) * sizeScale;
		float backArmLift = (isAttacking ? 2.2f : -runSwing * 1.3f) * sizeScale;
		Vector2 elbowFront = shoulderFront + new Vector2(faceSide.X * (unit.IsRanged ? 5.8f : 4.6f) * sizeScale, 4.2f * sizeScale + frontArmLift);
		Vector2 handFront = elbowFront + new Vector2(faceSide.X * (unit.IsRanged ? 6.8f : 5.6f) * sizeScale, (unit.IsRanged ? -0.8f - attackPose * 1.8f : 2f + frontArmLift * 0.35f) * sizeScale);
		Vector2 elbowBack = shoulderBack + new Vector2(-faceSide.X * 3.6f * sizeScale, 4.6f * sizeScale + backArmLift);
		Vector2 handBack = elbowBack + new Vector2(-faceSide.X * 2.2f * sizeScale, 4.2f * sizeScale + backArmLift * 0.45f);
		DrawLine(shoulderFront, elbowFront, outline, 3f);
		DrawLine(elbowFront, handFront, outline, 2.8f);
		DrawLine(shoulderFront, elbowFront, accent, 1.55f);
		DrawLine(elbowFront, handFront, accent, 1.4f);
		DrawLine(shoulderBack, elbowBack, outline, 2.6f);
		DrawLine(elbowBack, handBack, outline, 2.3f);
		DrawLine(shoulderBack, elbowBack, bodyColor.Lerp(Colors.Black, 0.2f), 1.3f);
		DrawLine(elbowBack, handBack, bodyColor.Lerp(Colors.Black, 0.24f), 1.2f);

		float stride = isRunning ? runSwing * 3.6f : (isAttacking ? attackPose * 1.4f : Mathf.Sin(runPhase * 0.45f) * 0.6f);
		Vector2 legLeftStart = hip + new Vector2(-torsoHalfWidth * 0.42f, 2.2f * sizeScale);
		Vector2 legRightStart = hip + new Vector2(torsoHalfWidth * 0.42f, 2.2f * sizeScale);
		Vector2 kneeLeft = legLeftStart + new Vector2((-1.2f + stride * 0.45f) * sizeScale, (5.8f - Mathf.Abs(stride) * 0.18f) * sizeScale);
		Vector2 kneeRight = legRightStart + new Vector2((1.2f - stride * 0.45f) * sizeScale, (5.8f - Mathf.Abs(stride) * 0.18f) * sizeScale);
		Vector2 footLeft = kneeLeft + new Vector2((-1.4f + stride * 0.35f) * sizeScale, (6.5f + Mathf.Abs(stride) * 0.1f) * sizeScale);
		Vector2 footRight = kneeRight + new Vector2((1.4f - stride * 0.35f) * sizeScale, (6.5f + Mathf.Abs(stride) * 0.1f) * sizeScale);
		DrawLine(legLeftStart, kneeLeft, outline, 3f);
		DrawLine(kneeLeft, footLeft, outline, 2.8f);
		DrawLine(legRightStart, kneeRight, outline, 3f);
		DrawLine(kneeRight, footRight, outline, 2.8f);
		DrawLine(legLeftStart, kneeLeft, bodyColor.Lerp(Colors.Black, 0.15f), 1.5f);
		DrawLine(kneeLeft, footLeft, bodyColor.Lerp(Colors.Black, 0.16f), 1.4f);
		DrawLine(legRightStart, kneeRight, bodyColor.Lerp(Colors.Black, 0.17f), 1.5f);
		DrawLine(kneeRight, footRight, bodyColor.Lerp(Colors.Black, 0.18f), 1.4f);

		if (unit.IsPlayerSide && !unit.IsHero && unit.SoldierClass == SoldierClass.Cavalry)
		{
			DrawRoomCavalryMount(hip, faceSide, outline, bodyColor.Lerp(Colors.Black, 0.18f), attackPose, sizeScale, runSwing);
		}

		if (unit.IsPlayerSide && !unit.IsHero)
		{
			DrawRoomSoldierClassSilhouette(unit, handFront, handBack, hip, faceSide, outline, accent, attackPose);
		}
		else if (unit.IsRanged)
		{
			DrawRoomRangedSilhouette(unit, handFront, handBack, faceSide, outline, accent, attackPose);
		}
		else
		{
			DrawRoomMeleeSilhouette(unit, handFront, handBack, hip, faceSide, outline, accent, attackPose);
		}
	}

	private float GetRoomAttackPose(RoomUnit unit)
	{
		if (unit.AttackWindupTime > 0f)
		{
			float totalWindup = unit.IsRanged ? 0.13f : (unit.IsHero || unit.IsElite ? 0.12f : 0.09f);
			return totalWindup > 0f ? 1f - Mathf.Clamp(unit.AttackWindupTime / totalWindup, 0f, 1f) : 1f;
		}

		if (unit.RecoveryTime > 0f)
		{
			float totalRecovery = unit.IsRanged ? 0.1f : 0.08f;
			return totalRecovery > 0f ? Mathf.Clamp(unit.RecoveryTime / totalRecovery, 0f, 1f) * 0.6f : 0f;
		}

		if (unit.HitPauseTime > 0f)
		{
			return 0.45f;
		}

		return 0f;
	}

	private void DrawRoomMeleeSilhouette(RoomUnit unit, Vector2 handFront, Vector2 handBack, Vector2 hip, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		DrawRoomBladeSilhouette(handFront, faceSide, outline, accent, attackPose);
	}

	private void DrawRoomRangedSilhouette(RoomUnit unit, Vector2 handFront, Vector2 handBack, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		Vector2 bowCenter = handFront + new Vector2(faceSide.X * (3.2f + attackPose * 1.8f), -1.6f - attackPose * 1.2f);
		float bowLen = unit.IsElite ? 8.6f : 7.8f;
		Vector2 bowTop = bowCenter + new Vector2(0f, -bowLen);
		Vector2 bowBottom = bowCenter + new Vector2(0f, bowLen);
		float bowStart = faceSide.X > 0f ? -1.25f : Mathf.Pi - 1.9f;
		float bowEnd = faceSide.X > 0f ? 1.25f : Mathf.Pi + 1.9f;
		DrawArc(bowCenter, bowLen * 0.72f, bowStart, bowEnd, 14, outline, 2.2f);
		DrawArc(bowCenter, bowLen * 0.72f, bowStart, bowEnd, 14, accent, 1.1f);
		DrawLine(bowTop, bowBottom, new Color(1f, 1f, 1f, 0.7f), 0.9f);

		Vector2 arrowStart = handBack + new Vector2(faceSide.X * 0.6f, -2.1f - attackPose * 0.8f);
		Vector2 arrowEnd = arrowStart + new Vector2(faceSide.X * (11f + (unit.AttackWindupTime > 0f ? 4f : 0f)), 0f);
		DrawLine(arrowStart, arrowEnd, outline, 2.4f);
		DrawLine(arrowStart, arrowEnd, accent, 1.2f);
		DrawLine(arrowEnd, arrowEnd + new Vector2(-faceSide.X * 3f, -1.8f), accent, 1f);
		DrawLine(arrowEnd, arrowEnd + new Vector2(-faceSide.X * 3f, 1.8f), accent, 1f);
	}

	private void DrawRoomSoldierClassSilhouette(RoomUnit unit, Vector2 handFront, Vector2 handBack, Vector2 hip, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		switch (unit.SoldierClass)
		{
			case SoldierClass.Recruit:
				DrawRoomRecruitSilhouette(handFront, handBack, faceSide, outline, accent, attackPose);
				break;
			case SoldierClass.Shield:
				DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, false, false);
				break;
			case SoldierClass.EliteShield:
				DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, true, false);
				break;
			case SoldierClass.ShieldPlusOne:
				DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, true, false);
				break;
			case SoldierClass.ShieldPlusTwo:
				DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, true, true);
				break;
			case SoldierClass.Pike:
				DrawRoomPikeSilhouette(handFront, handBack, faceSide, outline, accent, attackPose);
				break;
			case SoldierClass.Cavalry:
				DrawRoomCavalrySilhouette(handFront, handBack, hip, faceSide, outline, accent, attackPose);
				break;
			case SoldierClass.Archer:
				DrawRoomRangedSilhouette(unit, handFront, handBack, faceSide, outline, accent, attackPose);
				break;
			default:
				DrawRoomBladeSilhouette(handFront, faceSide, outline, accent, attackPose);
				break;
		}
	}

	private void DrawRoomBladeSilhouette(Vector2 handFront, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		float slashBias = attackPose;
		Vector2 weaponBase = handFront + new Vector2(faceSide.X * 1.6f, -0.8f);
		Vector2 weaponTip = weaponBase + new Vector2(faceSide.X * (10f + slashBias * 5f), -4.5f - slashBias * 4.2f);
		DrawLine(weaponBase, weaponTip, outline, 3f);
		DrawLine(weaponBase, weaponTip, accent, 1.6f);

		Vector2 guardA = weaponBase + new Vector2(0f, -2.6f);
		Vector2 guardB = weaponBase + new Vector2(0f, 2.6f);
		DrawLine(guardA, guardB, outline, 2f);
		DrawLine(guardA, guardB, accent.Lerp(Colors.White, 0.2f), 1f);
	}

	private void DrawRoomShieldSilhouette(Vector2 handFront, Vector2 handBack, Vector2 faceSide, Color outline, Color accent, float attackPose, bool eliteShield, bool ornateShield)
	{
		Vector2 shieldCenter = handFront + new Vector2(faceSide.X * (2.2f + attackPose * 0.45f), 2.9f - attackPose * 0.2f);
		float shieldH = ornateShield ? 16.8f : (eliteShield ? 14.6f : 13.2f);
		float shieldW = ornateShield ? 7.8f : (eliteShield ? 6.7f : 6f);
		Vector2[] shield =
		[
			shieldCenter + new Vector2(0f, -shieldH),
			shieldCenter + new Vector2(shieldW, -shieldH * 0.34f),
			shieldCenter + new Vector2(shieldW * 0.82f, shieldH * 0.42f),
			shieldCenter + new Vector2(0f, shieldH),
			shieldCenter + new Vector2(-shieldW * 0.82f, shieldH * 0.42f),
			shieldCenter + new Vector2(-shieldW, -shieldH * 0.34f),
		];
		DrawColoredPolygon(shield, accent.Lerp(Colors.Black, 0.34f));
		DrawPolyline(new[] { shield[0], shield[1], shield[2], shield[3], shield[4], shield[5], shield[0] }, outline, 1.2f);
		DrawLine(shieldCenter + new Vector2(0f, -shieldH * 0.72f), shieldCenter + new Vector2(0f, shieldH * 0.72f), accent.Lerp(Colors.White, 0.12f), 0.9f);
		if (ornateShield)
		{
			DrawLine(shieldCenter + new Vector2(-shieldW * 0.56f, -shieldH * 0.12f), shieldCenter + new Vector2(shieldW * 0.56f, -shieldH * 0.12f), accent.Lerp(Colors.White, 0.18f), 1f);
			DrawLine(shieldCenter + new Vector2(-shieldW * 0.4f, shieldH * 0.34f), shieldCenter + new Vector2(shieldW * 0.4f, shieldH * 0.34f), accent.Lerp(Colors.White, 0.14f), 0.9f);
		}

		Vector2 gripFront = shieldCenter + new Vector2(-faceSide.X * 3.6f, 2.2f);
		DrawLine(handFront, gripFront, outline, 2.4f);
		DrawLine(handFront, gripFront, accent, 1.1f);
	}

	private void DrawRoomRecruitSilhouette(Vector2 handFront, Vector2 handBack, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		Vector2 shaftStart = handBack.Lerp(handFront, 0.46f) + new Vector2(-faceSide.X * 1.2f, 2.2f);
		Vector2 shaftEnd = shaftStart + new Vector2(faceSide.X * (14f + attackPose * 4f), -8.5f - attackPose * 3f);
		DrawLine(shaftStart, shaftEnd, outline, 3f);
		DrawLine(shaftStart, shaftEnd, new Color(0.64f, 0.46f, 0.24f), 1.6f);
		Vector2 tineBase = shaftEnd;
		Vector2 tineDir = (shaftEnd - shaftStart).Normalized();
		Vector2 tineNormal = new(-tineDir.Y, tineDir.X);
		DrawLine(tineBase, tineBase + tineNormal * 4.2f, accent, 1f);
		DrawLine(tineBase, tineBase - tineNormal * 4.2f, accent, 1f);
		DrawLine(tineBase, tineBase + tineDir * 3.2f, accent, 1f);
	}

	private void DrawRoomPikeSilhouette(Vector2 handFront, Vector2 handBack, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		Vector2 shaftStart = handBack + new Vector2(-faceSide.X * 5f, 0.8f);
		Vector2 shaftEnd = handFront + new Vector2(faceSide.X * (16f + attackPose * 7f), -6.4f - attackPose * 2.6f);
		DrawLine(shaftStart, shaftEnd, outline, 3.2f);
		DrawLine(shaftStart, shaftEnd, new Color(0.64f, 0.46f, 0.24f), 1.8f);
		Vector2 spearBase = shaftEnd - new Vector2(faceSide.X * 5.2f, 0f);
		Vector2[] spearHead =
		[
			shaftEnd,
			spearBase + new Vector2(0f, -2.4f),
			spearBase + new Vector2(0f, 2.4f),
		];
		DrawColoredPolygon(spearHead, accent);
		DrawPolyline(new[] { spearHead[0], spearHead[1], spearHead[2], spearHead[0] }, outline, 1f);
	}

	private void DrawRoomCavalrySilhouette(Vector2 handFront, Vector2 handBack, Vector2 hip, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		Vector2 lanceStart = handBack.Lerp(handFront, 0.4f) + new Vector2(-faceSide.X * 2f, -1.2f);
		Vector2 lanceEnd = lanceStart + new Vector2(faceSide.X * (22f + attackPose * 8f), -6.2f - attackPose * 1.8f);
		DrawLine(lanceStart, lanceEnd, outline, 3.2f);
		DrawLine(lanceStart, lanceEnd, new Color(0.7f, 0.52f, 0.28f), 1.9f);
		Vector2 lanceHeadBase = lanceEnd - new Vector2(faceSide.X * 5.8f, 0f);
		Vector2[] lanceHead =
		[
			lanceEnd,
			lanceHeadBase + new Vector2(0f, -2.6f),
			lanceHeadBase + new Vector2(0f, 2.6f),
		];
		DrawColoredPolygon(lanceHead, accent.Lerp(Colors.White, 0.08f));
		DrawPolyline(new[] { lanceHead[0], lanceHead[1], lanceHead[2], lanceHead[0] }, outline, 1f);
	}

	private void DrawRoomCavalryMount(Vector2 hip, Vector2 faceSide, Color outline, Color bodyShade, float attackPose, float sizeScale, float runSwing)
	{
		Vector2 horseCenter = hip + new Vector2(0f, 10f * sizeScale);
		float bodyHalfW = 10f * sizeScale;
		float bodyHalfH = 4.2f * sizeScale;
		Rect2 bodyRect = new(horseCenter + new Vector2(-bodyHalfW, -bodyHalfH), new Vector2(bodyHalfW * 2f, bodyHalfH * 2f));
		DrawRect(bodyRect, bodyShade, true);
		DrawRect(bodyRect, outline, false, 1.2f);

		Vector2 chest = horseCenter + new Vector2(faceSide.X * (bodyHalfW + 3.4f * sizeScale), -1.6f * sizeScale);
		Vector2 nose = chest + new Vector2(faceSide.X * (6.2f * sizeScale + attackPose * 1.4f), -2.4f * sizeScale);
		DrawLine(chest, nose, outline, 3f);
		DrawLine(chest, nose, bodyShade.Lerp(Colors.White, 0.08f), 1.6f);
		DrawCircle(nose, 2.1f * sizeScale, bodyShade.Lerp(Colors.White, 0.12f));

		float legSwing = runSwing * 2.2f * sizeScale;
		Vector2 frontLegA = horseCenter + new Vector2(faceSide.X * 5.8f * sizeScale, bodyHalfH - 0.6f);
		Vector2 frontLegB = horseCenter + new Vector2(faceSide.X * 2.4f * sizeScale, bodyHalfH - 0.4f);
		Vector2 backLegA = horseCenter + new Vector2(-faceSide.X * 2.4f * sizeScale, bodyHalfH - 0.4f);
		Vector2 backLegB = horseCenter + new Vector2(-faceSide.X * 6f * sizeScale, bodyHalfH - 0.6f);
		DrawLine(frontLegA, frontLegA + new Vector2(faceSide.X * legSwing * 0.35f, 10f * sizeScale), outline, 2.2f);
		DrawLine(frontLegB, frontLegB + new Vector2(-faceSide.X * legSwing * 0.25f, 10f * sizeScale), outline, 2.2f);
		DrawLine(backLegA, backLegA + new Vector2(-faceSide.X * legSwing * 0.3f, 10f * sizeScale), outline, 2.2f);
		DrawLine(backLegB, backLegB + new Vector2(faceSide.X * legSwing * 0.28f, 10f * sizeScale), outline, 2.2f);
	}

	private void DrawRoomImpactEffects()
	{
		for (int i = 0; i < _roomProjectileEffects.Count; i++)
		{
			RoomProjectileEffect effect = _roomProjectileEffects[i];
			float lifeRatio = effect.Duration > 0f ? effect.TimeLeft / effect.Duration : 0f;
			float alpha = Mathf.Clamp(lifeRatio * 1.2f, 0f, 1f);
			Color color = effect.PlayerSide
				? new Color(0.8f, 0.94f, 1f, alpha * (effect.Heavy ? 0.98f : 0.9f))
				: new Color(1f, 0.82f, 0.74f, alpha * (effect.Heavy ? 0.98f : 0.9f));
			Vector2 head = effect.Position;
			Vector2 dir = effect.Velocity.Normalized();
			Vector2 tail = effect.PreviousPosition;
			if (dir != Vector2.Zero)
			{
				tail = head - dir * (effect.Heavy ? 28f : 22f);
			}

			DrawLine(tail, head, color, effect.Heavy ? 4.8f : 3.4f);
			DrawCircle(head, effect.Heavy ? 4.8f : 3.6f, new Color(1f, 1f, 1f, alpha * 0.95f));
			if (dir != Vector2.Zero)
			{
				Vector2 normal = new(-dir.Y, dir.X);
				Vector2 tip = head;
				DrawLine(tip, tip - dir * 9f + normal * 4f, color, 1.8f);
				DrawLine(tip, tip - dir * 9f - normal * 4f, color, 1.8f);
			}
		}

		for (int i = 0; i < _roomMeleeArcEffects.Count; i++)
		{
			RoomMeleeArcEffect effect = _roomMeleeArcEffects[i];
			float swingProgress = effect.Duration > 0.001f ? 1f - (effect.TimeLeft / effect.Duration) : 1f;
			float reveal = Mathf.Clamp(swingProgress / 0.42f, 0f, 1f);
			float collapse = swingProgress > 0.52f ? Mathf.Clamp((swingProgress - 0.52f) / 0.48f, 0f, 1f) : 0f;
			float visibleStrength = reveal * (1f - collapse);
			float alpha = Mathf.Clamp(Mathf.Sin(reveal * Mathf.Pi * 0.82f) * (1f - collapse * 0.92f), 0f, 1f);
			float arcStart = effect.FacingAngle - effect.ArcHalfAngle;
			float arcEnd = effect.FacingAngle + effect.ArcHalfAngle;
			float visibleStart = Mathf.Lerp(arcStart, arcEnd, collapse);
			float visibleEnd = Mathf.Lerp(arcStart, arcEnd, reveal);
			if (visibleEnd <= visibleStart)
			{
				continue;
			}

			Vector2 startDir = Vector2.Right.Rotated(visibleStart);
			Vector2 endDir = Vector2.Right.Rotated(visibleEnd);
			Vector2 startPos = effect.Origin + startDir * effect.Range;
			Vector2 endPos = effect.Origin + endDir * effect.Range;
			Color color = effect.PlayerSide
				? new Color(1f, 1f, 1f, alpha * (effect.Heavy ? 0.72f : 0.62f))
				: new Color(1f, 0.96f, 0.94f, alpha * (effect.Heavy ? 0.72f : 0.62f));
			float lineWidth = effect.Heavy ? 2.2f : 1.8f;
			float glowWidth = effect.Heavy ? 3.8f : 3.1f;
			DrawArc(effect.Origin, effect.Range, visibleStart, visibleEnd, 22, new Color(color.R, color.G, color.B, color.A * 0.14f), glowWidth);
			DrawArc(effect.Origin, effect.Range, visibleStart, visibleEnd, 22, color, lineWidth);
			DrawArc(effect.Origin, effect.Range - 1.2f, visibleStart + 0.01f, visibleEnd, 20, new Color(1f, 1f, 1f, alpha * 0.9f), effect.Heavy ? 1.05f : 0.9f);

			float startDotRadius = (effect.Heavy ? 1.6f : 1.35f) * visibleStrength;
			if (startDotRadius > 0.15f)
			{
				DrawCircle(startPos, startDotRadius, new Color(1f, 1f, 1f, alpha * 0.58f));
			}

			float tipDotRadius = (effect.Heavy ? 2.2f : 1.85f) * visibleStrength;
			if (tipDotRadius > 0.15f)
			{
				DrawCircle(endPos, tipDotRadius, new Color(1f, 1f, 1f, alpha * 0.95f));
			}

			Vector2 tangent = (endPos - startPos).Normalized();
			if (tangent != Vector2.Zero)
			{
				Vector2 tipBase = endPos - tangent * (effect.Heavy ? 6f : 5f);
				Vector2 tipNormal = new(-tangent.Y, tangent.X);
				float tipWidth = (effect.Heavy ? 2.6f : 2.2f) * visibleStrength;
				if (tipWidth > 0.08f)
				{
					Vector2[] tip =
					[
						endPos,
						tipBase + tipNormal * tipWidth,
						tipBase - tipNormal * tipWidth,
					];
					if (IsTriangleDrawable(tip[0], tip[1], tip[2]))
					{
						DrawColoredPolygon(tip, new Color(1f, 1f, 1f, alpha * 0.92f));
					}
				}
			}
		}

		for (int i = 0; i < _roomShockwaveEffects.Count; i++)
		{
			RoomShockwaveEffect effect = _roomShockwaveEffects[i];
			float lifeRatio = effect.Duration > 0f ? effect.TimeLeft / effect.Duration : 0f;
			float alpha = Mathf.Clamp(lifeRatio, 0f, 1f);
			Color outer = effect.PlayerSide
				? new Color(0.82f, 0.98f, 1f, alpha * 0.72f)
				: new Color(1f, 0.88f, 0.72f, alpha * 0.72f);
			Color inner = new Color(outer.R, outer.G, outer.B, alpha * 0.3f);
			Vector2 dir = effect.Direction == Vector2.Zero ? Vector2.Right : effect.Direction.Normalized();
			Vector2 normal = new(-dir.Y, dir.X);
			Vector2 routeStart = effect.Origin - dir * (effect.Length * 0.78f);
			for (int sample = 0; sample < 6; sample++)
			{
				float t = sample / 5f;
				Vector2 center = routeStart + dir * (effect.Length * t);
				float width = effect.Radius * (0.45f + 0.55f * Mathf.Sin(t * Mathf.Pi));
				DrawLine(center - normal * width, center + normal * width, outer, 2.6f);
				DrawLine(center - normal * (width * 0.62f), center + normal * (width * 0.62f), inner, 1.4f);
			}
		}
	}

	private static bool IsTriangleDrawable(Vector2 a, Vector2 b, Vector2 c)
	{
		float twiceArea = Mathf.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
		return twiceArea > 0.02f;
	}
}
