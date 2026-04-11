using Godot;

[GlobalClass]
public partial class AutoUnit : Node2D
{
	private enum CombatState
	{
		SearchTarget,
		SeekCover,
		HoldCover,
		PeekShoot,
		StrafeDuel,
		ChaseMelee,
	}

	[Signal]
	public delegate void FiredEventHandler(Vector2 from, Vector2 to, Color color, bool hit, bool isMelee);

	[Export]
	public string DisplayName { get; set; } = "Unit";

	[Export]
	public Color UnitColor { get; set; } = new(0.3f, 0.8f, 1.0f);

	[Export]
	public float BodyRadius { get; set; } = 18.0f;

	[Export]
	public float MaxMoveSpeed { get; set; } = 165.0f;

	[Export]
	public float TurnSpeed { get; set; } = 2.5f;

	[Export]
	public float ReverseSpeedFactor { get; set; } = 0.32f;

	[Export]
	public float ShotRange { get; set; } = 360.0f;

	[Export]
	public int MaxAmmo { get; set; } = 8;

	[Export]
	public float MeleeRange { get; set; } = 54.0f;

	[Export]
	public float MeleeArcDegrees { get; set; } = 90.0f;

	[Export]
	public float MeleeCooldown { get; set; } = 0.55f;

	[Export]
	public float AimToleranceDegrees { get; set; } = 8.0f;

	[Export]
	public float MinFireInterval { get; set; } = 0.45f;

	[Export]
	public float MaxFireInterval { get; set; } = 0.9f;

	[Export]
	public int MaxHp { get; set; } = 10;

	public AutoUnit Target { get; set; }
	public Rect2 ArenaRect { get; set; }
	public Rect2[] Obstacles { get; set; } = [];
	public bool IsAlive => CurrentHp > 0;
	public int CurrentHp { get; private set; }
	public int CurrentAmmo { get; private set; }
	public float FacingAngle { get; set; }
	public Vector2 FacingDirection => Vector2.Right.Rotated(FacingAngle);

	private readonly RandomNumberGenerator _random = new();
	private CombatState _state = CombatState.StrafeDuel;
	private float _fireCooldown;
	private float _strafeTimer;
	private float _strafeSign = 1.0f;
	private float _meleeSwingTimer;
	private float _meleeSwingDuration;
	private float _hitFlashTimer;
	private float _stateLockTimer;
	private float _peekExposureTimer;
	private float _targetLostTimer;
	private float _ignoreCoverTimer;
	private Rect2? _activeCoverObstacle;
	private Rect2? _ignoredCoverObstacle;
	private Vector2? _coverPoint;
	private Vector2? _peekPoint;
	private Vector2 _lastSeenTargetPosition;
	private bool _hasSeenTarget;

	public override void _Ready()
	{
		_random.Randomize();
		CurrentHp = MaxHp;
		CurrentAmmo = MaxAmmo;
		ResetCooldown();
		ResetStrafe();
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (!IsAlive)
		{
			return;
		}

		AutoUnit target = Target;
		if (target == null || !IsInstanceValid(target) || !target.IsAlive)
		{
			QueueRedraw();
			return;
		}

		float dt = (float)delta;
		UpdateAnimation(dt);
		UpdateVision(target, dt);
		UpdateState(target, dt);

		Vector2 targetFocus = GetTargetFocusPosition(target);
		Vector2 toTarget = targetFocus - GlobalPosition;
		if (toTarget.LengthSquared() > 0.001f)
		{
			float desiredFacing = toTarget.Angle();
			FacingAngle = Mathf.RotateToward(FacingAngle, desiredFacing, TurnSpeed * dt);
		}

		UpdateMovement(target, dt);
		UpdateFire(target, dt);
		ResolveUnitSeparation(target);
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (!IsAlive)
		{
			DrawCorpse();
			return;
		}

		DrawHealthBar();
		DrawBody();
	}

	public bool IsAimingAt(AutoUnit other, float extraToleranceDegrees = 6.0f)
	{
		if (other == null || !other.IsAlive)
		{
			return false;
		}

		Vector2 toOther = other.GlobalPosition - GlobalPosition;
		if (toOther.LengthSquared() < 0.001f)
		{
			return true;
		}

		float angleDiff = Mathf.Abs(Mathf.Wrap(toOther.Angle() - FacingAngle, -Mathf.Pi, Mathf.Pi));
		return angleDiff <= Mathf.DegToRad(AimToleranceDegrees + extraToleranceDegrees);
	}

	public bool IsInMeleeArc(AutoUnit other)
	{
		if (other == null || !other.IsAlive)
		{
			return false;
		}

		Vector2 toOther = other.GlobalPosition - GlobalPosition;
		float rangeLimit = MeleeRange + other.BodyRadius;
		if (toOther.Length() > rangeLimit)
		{
			return false;
		}

		float halfArc = Mathf.DegToRad(MeleeArcDegrees * 0.5f);
		float angleDiff = Mathf.Abs(Mathf.Wrap(toOther.Angle() - FacingAngle, -Mathf.Pi, Mathf.Pi));
		return angleDiff <= halfArc;
	}

	public bool HasLineOfSightTo(Vector2 point)
	{
		return !TryGetNearestObstacleHit(GlobalPosition, point, out _, out _);
	}

	public bool CanSeeTarget(AutoUnit other)
	{
		if (other == null || !other.IsAlive)
		{
			return false;
		}

		return HasLineOfSightTo(other.GlobalPosition);
	}

	public bool HasClearShotTo(AutoUnit other)
	{
		if (other == null || !other.IsAlive)
		{
			return false;
		}

		Vector2 toOther = other.GlobalPosition - GlobalPosition;
		float alongRay = toOther.Dot(FacingDirection);
		if (alongRay <= 0.0f || alongRay > ShotRange)
		{
			return false;
		}

		if (!IsAimingAt(other, 0.0f))
		{
			return false;
		}

		return !TryGetNearestObstacleHit(GlobalPosition, other.GlobalPosition, out _, out _);
	}

	public void ApplyDamage(int amount)
	{
		if (!IsAlive)
		{
			return;
		}

		CurrentHp = Mathf.Max(0, CurrentHp - amount);
		_hitFlashTimer = 0.16f;
		QueueRedraw();
	}

	private void UpdateMovement(AutoUnit target, float dt)
	{
		_strafeTimer -= dt;
		if (_strafeTimer <= 0.0f)
		{
			ResetStrafe();
		}

		Vector2 targetFocus = GetTargetFocusPosition(target);
		Vector2 toTarget = targetFocus - GlobalPosition;
		float distance = toTarget.Length();
		Vector2 targetDirection = distance > 0.001f ? toTarget / distance : Vector2.Zero;
		Vector2 strafeDirection = new Vector2(-targetDirection.Y, targetDirection.X) * _strafeSign;
		bool hasAmmo = CurrentAmmo > 0;
		bool enemyHasAmmo = target.CurrentAmmo > 0;
		bool canSeeTarget = CanSeeTarget(target);
		bool selfExposed = target.HasLineOfSightTo(GlobalPosition);
		bool clearShot = canSeeTarget && HasClearShotTo(target);
		bool hasUsableCover = _activeCoverObstacle.HasValue && _coverPoint.HasValue && _peekPoint.HasValue;

		Vector2 desiredMove = _state switch
		{
			CombatState.SearchTarget => MoveTowardPoint(_hasSeenTarget ? _lastSeenTargetPosition : targetFocus, 1.2f),
			CombatState.SeekCover => MoveTowardPoint(_coverPoint, 1.95f),
			CombatState.HoldCover => HoldAroundCover(target),
			CombatState.PeekShoot => MoveTowardPoint(_peekPoint, 1.55f),
			CombatState.ChaseMelee => targetDirection * 1.6f + strafeDirection * 0.35f,
			_ => strafeDirection * 0.95f,
		};

		if (_state == CombatState.StrafeDuel)
		{
			if (hasAmmo && !hasUsableCover)
			{
				float preferredDistance = CurrentHp >= target.CurrentHp ? ShotRange * 0.72f : ShotRange * 0.88f;
				float minComfortDistance = ShotRange * 0.45f;

				if (distance > preferredDistance)
				{
					desiredMove += targetDirection * 1.1f;
				}
				else if (distance < minComfortDistance)
				{
					desiredMove -= targetDirection * 1.15f;
				}
			}
			else if (distance > MeleeRange * 0.8f)
			{
				desiredMove += targetDirection * 1.35f;
			}
		}

		if ((_state == CombatState.StrafeDuel || _state == CombatState.PeekShoot) && enemyHasAmmo && target.IsAimingAt(this) && distance <= target.ShotRange + 30.0f && selfExposed)
		{
			desiredMove += strafeDirection * 1.45f;
			desiredMove -= targetDirection * 0.55f;
		}

		if (_state == CombatState.StrafeDuel && CurrentHp <= 4 && hasAmmo)
		{
			desiredMove += strafeDirection * 0.4f;
			desiredMove -= targetDirection * 0.45f;
		}

		if (_state == CombatState.StrafeDuel && !enemyHasAmmo && hasAmmo)
		{
			desiredMove -= targetDirection * 0.55f;
		}

		if (_state == CombatState.StrafeDuel && !hasAmmo && enemyHasAmmo && _state != CombatState.ChaseMelee)
		{
			desiredMove += targetDirection * 0.4f;
		}

		if (_state == CombatState.HoldCover && hasUsableCover)
		{
			desiredMove *= 0.35f;
		}

		if (desiredMove.LengthSquared() < 0.001f)
		{
			return;
		}

		Vector2 moveDirection = desiredMove.Normalized();
		float moveAngle = Mathf.Abs(Mathf.Wrap(moveDirection.Angle() - FacingAngle, -Mathf.Pi, Mathf.Pi));
		float moveFactor = Mathf.Lerp(1.0f, ReverseSpeedFactor, moveAngle / Mathf.Pi);
		Vector2 desiredPosition = GlobalPosition + moveDirection * MaxMoveSpeed * moveFactor * dt;
		GlobalPosition = ResolveMovement(GlobalPosition, desiredPosition);
	}

	private void UpdateState(AutoUnit target, float dt)
	{
		_stateLockTimer = Mathf.Max(0.0f, _stateLockTimer - dt);
		_ignoreCoverTimer = Mathf.Max(0.0f, _ignoreCoverTimer - dt);
		if (_ignoreCoverTimer <= 0.0f)
		{
			_ignoredCoverObstacle = null;
		}

		bool hasAmmo = CurrentAmmo > 0;
		bool enemyHasAmmo = target.CurrentAmmo > 0;
		bool canSeeTarget = CanSeeTarget(target);
		bool selfExposed = target.HasLineOfSightTo(GlobalPosition);
		bool clearShot = canSeeTarget && HasClearShotTo(target);
		float distance = GlobalPosition.DistanceTo(GetTargetFocusPosition(target));

		_activeCoverObstacle = enemyHasAmmo ? FindBestCoverObstacle(target) : null;
		_coverPoint = _activeCoverObstacle.HasValue ? FindCoverPointBehindObstacle(target, _activeCoverObstacle.Value) : null;
		_peekPoint = _activeCoverObstacle.HasValue ? FindPeekPoint(target, _activeCoverObstacle.Value) : null;
		bool hasUsableCover = _activeCoverObstacle.HasValue && _coverPoint.HasValue && _peekPoint.HasValue;
		bool nearCoverPoint = _coverPoint.HasValue && GlobalPosition.DistanceTo(_coverPoint.Value) <= 18.0f;
		bool nearPeekPoint = _peekPoint.HasValue && GlobalPosition.DistanceTo(_peekPoint.Value) <= 18.0f;
		if (_state == CombatState.PeekShoot && selfExposed)
		{
			_peekExposureTimer += dt;
		}
		else
		{
			_peekExposureTimer = 0.0f;
		}

		if (_stateLockTimer > 0.0f)
		{
			return;
		}

		CombatState nextState;
		if (!canSeeTarget && _hasSeenTarget)
		{
			nextState = CombatState.SearchTarget;
		}
		else if (!hasAmmo)
		{
			nextState = CombatState.ChaseMelee;
		}
		else if (enemyHasAmmo && hasUsableCover && selfExposed)
		{
			nextState = CombatState.SeekCover;
		}
		else if (_state == CombatState.SeekCover && nearCoverPoint)
		{
			nextState = CombatState.HoldCover;
		}
		else if (_state == CombatState.PeekShoot && (!nearPeekPoint || !clearShot || distance > ShotRange * 0.96f || _peekExposureTimer >= 0.32f))
		{
			nextState = clearShot ? CombatState.HoldCover : CombatState.SearchTarget;
		}
		else if (_state == CombatState.HoldCover && hasUsableCover && nearCoverPoint && !selfExposed)
		{
			nextState = CombatState.PeekShoot;
		}
		else if (_state == CombatState.HoldCover && hasUsableCover && canSeeTarget && !clearShot)
		{
			nextState = CombatState.PeekShoot;
		}
		else if (_state == CombatState.HoldCover && !selfExposed)
		{
			nextState = CombatState.HoldCover;
		}
		else if (enemyHasAmmo && hasUsableCover)
		{
			nextState = nearCoverPoint ? CombatState.HoldCover : CombatState.SeekCover;
		}
		else if (_peekPoint.HasValue && !clearShot && distance <= ShotRange * 0.92f)
		{
			nextState = CombatState.PeekShoot;
		}
		else
		{
			nextState = CombatState.StrafeDuel;
		}

		if (nextState != _state)
		{
			if (_state == CombatState.PeekShoot && nextState == CombatState.SearchTarget && _activeCoverObstacle.HasValue)
			{
				_ignoredCoverObstacle = _activeCoverObstacle;
				_ignoreCoverTimer = 1.1f;
			}

			_state = nextState;
			_peekExposureTimer = 0.0f;
			_stateLockTimer = nextState switch
			{
				CombatState.SearchTarget => 0.4f,
				CombatState.HoldCover => 0.35f,
				CombatState.PeekShoot => 0.28f,
				CombatState.SeekCover => 0.3f,
				CombatState.ChaseMelee => 0.22f,
				_ => 0.18f,
			};
		}
	}

	private void UpdateFire(AutoUnit target, float dt)
	{
		_fireCooldown -= dt;
		if (_fireCooldown > 0.0f)
		{
			return;
		}

		if (CurrentAmmo > 0)
		{
			if (!CanSeeTarget(target))
			{
				_fireCooldown = 0.08f;
				return;
			}

			Vector2 from = GlobalPosition;
			Vector2 maxTo = from + FacingDirection * ShotRange;
			bool hasAim = IsAimingAt(target, 0.0f);
			Vector2 toTarget = target.GlobalPosition - from;
			float targetAlongRay = toTarget.Dot(FacingDirection);
			bool withinRange = targetAlongRay > 0.0f && targetAlongRay <= ShotRange;
			bool blockedBeforeTarget = TryGetNearestObstacleHit(from, maxTo, out Vector2 obstacleHit, out float obstacleDistance) && obstacleDistance < targetAlongRay;
			bool hit = hasAim && withinRange && !blockedBeforeTarget && DistanceToSegment(target.GlobalPosition, from, from + FacingDirection * targetAlongRay) <= target.BodyRadius + 3.0f;
			Vector2 to = blockedBeforeTarget ? obstacleHit : (hit ? target.GlobalPosition : maxTo);

			if (!hasAim || !withinRange || blockedBeforeTarget)
			{
				_fireCooldown = 0.08f;
				return;
			}

			CurrentAmmo -= 1;
			if (hit)
			{
				target.ApplyDamage(1);
			}

			EmitSignal(SignalName.Fired, from, to, UnitColor, hit, false);
			ResetCooldown();
			if (_state == CombatState.PeekShoot)
			{
				_state = CombatState.HoldCover;
				_stateLockTimer = 0.4f;
				_peekExposureTimer = 0.0f;
			}
			QueueRedraw();
		}
		else
		{
			if (!CanSeeTarget(target))
			{
				_fireCooldown = 0.1f;
				return;
			}

			bool hit = IsInMeleeArc(target);
			Vector2 from = GlobalPosition;
			Vector2 to = from + FacingDirection * MeleeRange;

			if (!hit)
			{
				_fireCooldown = 0.1f;
				return;
			}

			target.ApplyDamage(1);
			EmitSignal(SignalName.Fired, from, to, UnitColor, true, true);
			_fireCooldown = MeleeCooldown;
			_meleeSwingDuration = 0.2f;
			_meleeSwingTimer = _meleeSwingDuration;
		}
	}

	private void DrawBody()
	{
		Vector2 forward = FacingDirection;
		Vector2 right = new Vector2(-forward.Y, forward.X);
		float swingProgress = _meleeSwingDuration > 0.001f ? 1.0f - (_meleeSwingTimer / _meleeSwingDuration) : 0.0f;
		float lungeAmount = _meleeSwingTimer > 0.0f ? Mathf.Sin(swingProgress * Mathf.Pi) * 9.0f : 0.0f;
		Vector2 drawOffset = forward * lungeAmount;
		Color bodyColor = _hitFlashTimer > 0.0f ? UnitColor.Lightened(0.55f) : UnitColor;

		Vector2[] hull =
		[
			drawOffset + forward * (BodyRadius + 8.0f),
			drawOffset + -forward * (BodyRadius * 0.75f) + right * (BodyRadius * 0.8f),
			drawOffset + -forward * (BodyRadius * 0.45f),
			drawOffset + -forward * (BodyRadius * 0.75f) - right * (BodyRadius * 0.8f),
		];

		DrawColoredPolygon(hull, bodyColor);
		DrawPolyline(hull, Colors.White, 2.0f);
		DrawLine(drawOffset, drawOffset + forward * (BodyRadius + 14.0f), Colors.White, 3.0f);
		DrawCircle(drawOffset, 5.0f, Colors.White);

		if (_meleeSwingTimer > 0.0f)
		{
			DrawMeleeSlash(forward, drawOffset, swingProgress);
		}
	}

	private void DrawCorpse()
	{
		Color corpseColor = UnitColor.Darkened(0.45f);
		Vector2 right = new Vector2(-FacingDirection.Y, FacingDirection.X);
		Vector2 a = -FacingDirection * (BodyRadius * 0.9f) + right * (BodyRadius * 0.45f);
		Vector2 b = FacingDirection * (BodyRadius * 0.9f) - right * (BodyRadius * 0.45f);
		Vector2 c = -FacingDirection * (BodyRadius * 0.9f) - right * (BodyRadius * 0.45f);
		Vector2 d = FacingDirection * (BodyRadius * 0.9f) + right * (BodyRadius * 0.45f);

		DrawLine(a, b, corpseColor, 4f);
		DrawLine(c, d, corpseColor, 4f);
		DrawCircle(Vector2.Zero, 4f, corpseColor.Lightened(0.15f));
	}

	private void DrawHealthBar()
	{
		const float width = 42.0f;
		const float height = 6.0f;
		Vector2 topLeft = new(-width * 0.5f, -BodyRadius - 22.0f);
		float hpRatio = MaxHp > 0 ? (float)CurrentHp / MaxHp : 0.0f;

		DrawRect(new Rect2(topLeft, new Vector2(width, height)), new Color(0.18f, 0.18f, 0.2f), true);
		DrawRect(new Rect2(topLeft, new Vector2(width * hpRatio, height)), new Color(0.35f, 0.95f, 0.45f), true);
		DrawRect(new Rect2(topLeft, new Vector2(width, height)), Colors.White, false, 1.5f);

		float ammoRatio = MaxAmmo > 0 ? (float)CurrentAmmo / MaxAmmo : 0.0f;
		Vector2 ammoTopLeft = topLeft + new Vector2(0.0f, 10.0f);
		DrawRect(new Rect2(ammoTopLeft, new Vector2(width, 4.0f)), new Color(0.16f, 0.16f, 0.18f), true);
		DrawRect(new Rect2(ammoTopLeft, new Vector2(width * ammoRatio, 4.0f)), new Color(0.95f, 0.8f, 0.28f), true);
		DrawRect(new Rect2(ammoTopLeft, new Vector2(width, 4.0f)), Colors.White, false, 1.0f);

		DrawString(ThemeDB.FallbackFont, topLeft + new Vector2(-6.0f, -6.0f), GetStateLabel(), HorizontalAlignment.Left, -1.0f, 11, Colors.White);
	}

	private Rect2 GetArenaShrinkRect()
	{
		return ArenaRect.GrowIndividual(-BodyRadius - 12.0f, -BodyRadius - 12.0f, -BodyRadius - 12.0f, -BodyRadius - 12.0f);
	}

	private Vector2 ClampToArena(Vector2 position)
	{
		Rect2 rect = GetArenaShrinkRect();
		return new Vector2(
			Mathf.Clamp(position.X, rect.Position.X, rect.End.X),
			Mathf.Clamp(position.Y, rect.Position.Y, rect.End.Y));
	}

	private void ResetCooldown()
	{
		_fireCooldown = _random.RandfRange(MinFireInterval, MaxFireInterval);
	}

	private void ResetStrafe()
	{
		_strafeTimer = _random.RandfRange(0.7f, 1.7f);
		_strafeSign = _random.Randf() > 0.5f ? 1.0f : -1.0f;
	}

	private void UpdateAnimation(float dt)
	{
		if (_meleeSwingTimer > 0.0f)
		{
			_meleeSwingTimer = Mathf.Max(0.0f, _meleeSwingTimer - dt);
		}

		if (_hitFlashTimer > 0.0f)
		{
			_hitFlashTimer = Mathf.Max(0.0f, _hitFlashTimer - dt);
		}
	}

	private void DrawMeleeSlash(Vector2 forward, Vector2 drawOffset, float swingProgress)
	{
		float halfArc = Mathf.DegToRad(MeleeArcDegrees * 0.5f);
		float sweep = Mathf.Lerp(-halfArc, halfArc, swingProgress);
		float slashAngle = FacingAngle + sweep;
		Vector2 slashDir = Vector2.Right.Rotated(slashAngle);
		Vector2 outer = drawOffset + slashDir * (MeleeRange + 6.0f);
		Vector2 innerLeft = drawOffset + Vector2.Right.Rotated(slashAngle - 0.18f) * (MeleeRange * 0.45f);
		Vector2 innerRight = drawOffset + Vector2.Right.Rotated(slashAngle + 0.18f) * (MeleeRange * 0.45f);
		Color slashColor = new Color(1.0f, 1.0f, 1.0f, Mathf.Sin(swingProgress * Mathf.Pi) * 0.8f);

		Vector2[] slash =
		[
			outer,
			innerLeft,
			drawOffset,
			innerRight,
		];

		DrawColoredPolygon(slash, slashColor);
		DrawArc(drawOffset, MeleeRange, FacingAngle - halfArc, FacingAngle + halfArc, 18, slashColor, 3.0f);
	}

	private Rect2? FindBestCoverObstacle(AutoUnit target)
	{
		if (Obstacles == null || Obstacles.Length == 0)
		{
			return null;
		}

		float bestScore = float.MaxValue;
		Rect2 bestObstacle = default;

		foreach (Rect2 obstacle in Obstacles)
		{
			if (_ignoredCoverObstacle.HasValue && IsSameObstacle(obstacle, _ignoredCoverObstacle.Value))
			{
				continue;
			}

			if (!SegmentIntersectsRect(target.GlobalPosition, GlobalPosition, obstacle))
			{
				continue;
			}

			float score = GlobalPosition.DistanceSquaredTo(obstacle.GetCenter());
			if (score < bestScore)
			{
				bestScore = score;
				bestObstacle = obstacle;
			}
		}

		return bestScore < float.MaxValue ? bestObstacle : null;
	}

	private Vector2? FindCoverPointBehindObstacle(AutoUnit target, Rect2 obstacle)
	{
		Vector2 obstacleCenter = obstacle.GetCenter();
		Vector2 awayFromTarget = obstacleCenter - target.GlobalPosition;
		if (awayFromTarget.LengthSquared() < 0.01f)
		{
			return null;
		}

		Vector2 dir = awayFromTarget.Normalized();
		Vector2 candidate = obstacleCenter + dir * (Mathf.Max(obstacle.Size.X, obstacle.Size.Y) * 0.65f + BodyRadius + 18.0f);
		candidate = ClampToArena(candidate);
		return PushOutsideObstacle(candidate, obstacle);
	}

	private Vector2? FindPeekPoint(AutoUnit target, Rect2 obstacle)
	{
		Vector2 obstacleCenter = obstacle.GetCenter();
		Vector2 toTarget = (target.GlobalPosition - obstacleCenter).Normalized();
		Vector2 side = new(-toTarget.Y, toTarget.X);
		float offset = Mathf.Max(obstacle.Size.X, obstacle.Size.Y) * 0.55f + BodyRadius + 16.0f;

		Vector2 candidateA = PushOutsideObstacle(ClampToArena(obstacleCenter + side * offset), obstacle);
		Vector2 candidateB = PushOutsideObstacle(ClampToArena(obstacleCenter - side * offset), obstacle);

		bool shotA = !TryGetNearestObstacleHit(candidateA, target.GlobalPosition, out _, out _);
		bool shotB = !TryGetNearestObstacleHit(candidateB, target.GlobalPosition, out _, out _);

		if (shotA && !shotB)
		{
			return candidateA;
		}

		if (shotB && !shotA)
		{
			return candidateB;
		}

		return GlobalPosition.DistanceSquaredTo(candidateA) <= GlobalPosition.DistanceSquaredTo(candidateB) ? candidateA : candidateB;
	}

	private Vector2 HoldAroundCover(AutoUnit target)
	{
		if (!_coverPoint.HasValue)
		{
			return Vector2.Zero;
		}

		Vector2 toCover = _coverPoint.Value - GlobalPosition;
		if (toCover.LengthSquared() > 36.0f)
		{
			return toCover.Normalized() * 1.4f;
		}

		Vector2 awayFromTarget = (_coverPoint.Value - target.GlobalPosition).Normalized();
		return awayFromTarget * 0.22f;
	}

	private Vector2 MoveTowardPoint(Vector2? point, float strength)
	{
		if (!point.HasValue)
		{
			return Vector2.Zero;
		}

		Vector2 offset = point.Value - GlobalPosition;
		if (offset.LengthSquared() <= 4.0f)
		{
			return Vector2.Zero;
		}

		return offset.Normalized() * strength;
	}

	private Vector2 MoveTowardPoint(Vector2 point, float strength)
	{
		Vector2 offset = point - GlobalPosition;
		if (offset.LengthSquared() <= 4.0f)
		{
			return Vector2.Zero;
		}

		return offset.Normalized() * strength;
	}

	private void ResolveUnitSeparation(AutoUnit target)
	{
		if (target == null || !target.IsAlive)
		{
			return;
		}

		float minimumDistance = BodyRadius + target.BodyRadius + 6.0f;
		Vector2 offset = GlobalPosition - target.GlobalPosition;
		float distance = offset.Length();
		if (distance >= minimumDistance)
		{
			return;
		}

		Vector2 pushDirection = distance > 0.001f ? offset / distance : -FacingDirection;
		Vector2 resolved = target.GlobalPosition + pushDirection * minimumDistance;
		GlobalPosition = ResolveMovement(GlobalPosition, resolved);
	}

	private string GetStateLabel()
	{
		return _state switch
		{
			CombatState.SearchTarget => "搜索目标",
			CombatState.SeekCover => "找掩体",
			CombatState.HoldCover => "躲掩体",
			CombatState.PeekShoot => "探头射击",
			CombatState.StrafeDuel => "对枪移动",
			CombatState.ChaseMelee => "近战追击",
			_ => "待机",
		};
	}

	private static bool IsSameObstacle(Rect2 a, Rect2 b)
	{
		return a.Position == b.Position && a.Size == b.Size;
	}

	private void UpdateVision(AutoUnit target, float dt)
	{
		if (CanSeeTarget(target))
		{
			_lastSeenTargetPosition = target.GlobalPosition;
			_hasSeenTarget = true;
			_targetLostTimer = 0.0f;
		}
		else if (_hasSeenTarget)
		{
			_targetLostTimer += dt;
			if (_targetLostTimer >= 2.0f)
			{
				_hasSeenTarget = false;
			}
		}
	}

	private Vector2 GetTargetFocusPosition(AutoUnit target)
	{
		if (CanSeeTarget(target))
		{
			return target.GlobalPosition;
		}

		if (_hasSeenTarget)
		{
			return _lastSeenTargetPosition;
		}

		return target.GlobalPosition;
	}

	private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
	{
		Vector2 segment = b - a;
		float lengthSquared = segment.LengthSquared();
		if (lengthSquared <= 0.0001f)
		{
			return point.DistanceTo(a);
		}

		float t = Mathf.Clamp((point - a).Dot(segment) / lengthSquared, 0.0f, 1.0f);
		Vector2 projection = a + segment * t;
		return point.DistanceTo(projection);
	}

	private bool TryGetNearestObstacleHit(Vector2 from, Vector2 to, out Vector2 hitPoint, out float hitDistance)
	{
		hitPoint = Vector2.Zero;
		hitDistance = float.MaxValue;
		bool found = false;

		if (Obstacles == null)
		{
			return false;
		}

		foreach (Rect2 obstacle in Obstacles)
		{
			if (TryIntersectSegmentRect(from, to, obstacle, out Vector2 candidate))
			{
				float distance = from.DistanceTo(candidate);
				if (distance < hitDistance)
				{
					hitDistance = distance;
					hitPoint = candidate;
					found = true;
				}
			}
		}

		return found;
	}

	private Vector2 ResolveMovement(Vector2 from, Vector2 desired)
	{
		Vector2 clamped = ClampToArena(desired);
		if (Obstacles == null || Obstacles.Length == 0)
		{
			return clamped;
		}

		Vector2 current = PushOutsideAllObstacles(from);
		Vector2 next = PushOutsideAllObstacles(clamped);

		foreach (Rect2 obstacle in Obstacles)
		{
			Rect2 expanded = ExpandObstacle(obstacle);
			if (SegmentIntersectsRect(current, next, expanded) || expanded.HasPoint(next))
			{
				Vector2 slide = new(next.X, current.Y);
				if (!expanded.HasPoint(slide) && !SegmentIntersectsRect(current, slide, expanded))
				{
					next = slide;
				}
				else
				{
					slide = new(current.X, next.Y);
					if (!expanded.HasPoint(slide) && !SegmentIntersectsRect(current, slide, expanded))
					{
						next = slide;
					}
					else
					{
						next = current;
					}
				}
			}
		}

		return PushOutsideAllObstacles(next);
	}

	private Vector2 PushOutsideAllObstacles(Vector2 point)
	{
		Vector2 adjusted = point;
		if (Obstacles == null)
		{
			return adjusted;
		}

		foreach (Rect2 obstacle in Obstacles)
		{
			adjusted = PushOutsideObstacle(adjusted, obstacle);
		}

		return adjusted;
	}

	private Vector2 PushOutsideObstacle(Vector2 point, Rect2 obstacle)
	{
		Rect2 expanded = ExpandObstacle(obstacle);
		if (!expanded.HasPoint(point))
		{
			return point;
		}

		float left = Mathf.Abs(point.X - expanded.Position.X);
		float right = Mathf.Abs(expanded.End.X - point.X);
		float top = Mathf.Abs(point.Y - expanded.Position.Y);
		float bottom = Mathf.Abs(expanded.End.Y - point.Y);
		float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
		const float epsilon = 0.5f;

		if (min == left)
		{
			point.X = expanded.Position.X - epsilon;
		}
		else if (min == right)
		{
			point.X = expanded.End.X + epsilon;
		}
		else if (min == top)
		{
			point.Y = expanded.Position.Y - epsilon;
		}
		else
		{
			point.Y = expanded.End.Y + epsilon;
		}

		return ClampToArena(point);
	}

	private Rect2 ExpandObstacle(Rect2 obstacle)
	{
		return obstacle.Grow(BodyRadius + 2.0f);
	}

	private static bool SegmentIntersectsRect(Vector2 from, Vector2 to, Rect2 rect)
	{
		return TryIntersectSegmentRect(from, to, rect, out _);
	}

	private static bool TryIntersectSegmentRect(Vector2 from, Vector2 to, Rect2 rect, out Vector2 hitPoint)
	{
		hitPoint = Vector2.Zero;
		if (rect.HasPoint(from))
		{
			hitPoint = from;
			return true;
		}

		Vector2[] corners =
		[
			rect.Position,
			new Vector2(rect.End.X, rect.Position.Y),
			rect.End,
			new Vector2(rect.Position.X, rect.End.Y),
		];

		bool found = false;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < corners.Length; i++)
		{
			Vector2 a = corners[i];
			Vector2 b = corners[(i + 1) % corners.Length];
			Variant intersectionVariant = Geometry2D.SegmentIntersectsSegment(from, to, a, b);
			if (!intersectionVariant.VariantType.Equals(Variant.Type.Nil))
			{
				Vector2 intersection = intersectionVariant.AsVector2();
				float distance = from.DistanceTo(intersection);
				if (distance < bestDistance)
				{
					bestDistance = distance;
					hitPoint = intersection;
					found = true;
				}
			}
		}

		return found;
	}
}
