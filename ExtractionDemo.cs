using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class ExtractionDemo : Node2D
{
	private const float NavCellSize = 44f;
	private const float ManualLootRadius = 110f;
	private const float CommandReachRadius = 40f;
	private const float LootInteractRadius = 78f;
	private const float LootWorkerStopRadius = 38f;

	private enum TeamState { Move, Fight, Loot, Extract }
	private enum EnemyState { Patrol, Investigate, Engage }
	private enum ItemRarity { White, Green, Blue, Purple, Gold, Red }

	private sealed class Unit
	{
		public string Name = "";
		public Vector2 Position;
		public Vector2 Facing = Vector2.Right;
		public Vector2 Offset;
		public float Radius = 13f;
		public float Speed = 145f;
		public float Vision = 245f;
		public float Range = 220f;
		public float Cooldown;
		public int Hp = 8;
		public int MaxHp = 8;
		public int Ammo = 6;
		public int Medkits;
		public bool SearchEnabled = true;
		public bool RunEnabled = true;
		public bool IsRunning;
		public bool IsMoving;
		public float MaxStamina = 240f;
		public float Stamina = 240f;
		public float StaminaRegen = 4f;
		public float StaminaDrain = 10f;
		public float RunSpeedMultiplier = 1.5f;
		public float Accuracy = 0.78f;
		public float HeadshotChance = 0.14f;
		public bool Alive => Hp > 0;
	}

	private sealed class Enemy
	{
		public Vector2 Position;
		public Vector2 Facing = Vector2.Right;
		public Vector2[] Patrol = [];
		public int PatrolIndex;
		public Vector2 LastSeen;
		public EnemyState State = EnemyState.Patrol;
		public float StateTimer;
		public float Cooldown;
		public bool IsMoving;
		public float Accuracy = 0.68f;
		public float HeadshotChance = 0.1f;
		public int Hp = 4;
		public bool Alive => Hp > 0;
	}

	private readonly struct ShotRoll
	{
		public ShotRoll(bool hit, bool headshot, Vector2 traceEnd)
		{
			Hit = hit;
			Headshot = headshot;
			TraceEnd = traceEnd;
		}

		public bool Hit { get; }
		public bool Headshot { get; }
		public Vector2 TraceEnd { get; }
	}

	private sealed class Loot
	{
		public string Label = "";
		public Vector2 Position;
		public int Value;
		public bool Seen;
		public bool Taken;
		public Vector2I GridSize;
		public readonly List<LootItem> Items = new();
	}

	private sealed class LootItem
	{
		public string Label = "";
		public ItemRarity Rarity;
		public Vector2I Size;
		public Vector2I Cell;
		public int Value;
		public float SearchTime;
		public float SearchProgress;
		public bool Revealed;
		public Unit Searcher;
	}

	private sealed class Fx
	{
		public Vector2 From;
		public Vector2 To;
		public Color Color;
		public float TimeLeft;
	}

	private readonly List<Unit> _squad = new();
	private readonly List<Enemy> _enemies = new();
	private readonly List<Loot> _loot = new();
	private readonly List<Fx> _fx = new();
	private readonly Rect2 _map = new(new Vector2(70, 70), new Vector2(1010, 560));
	private readonly Rect2 _extract = new(new Vector2(900, 500), new Vector2(180, 130));
	private readonly Rect2[] _searchToggleRects = new Rect2[3];
	private readonly Rect2[] _runToggleRects = new Rect2[3];
	private readonly RandomNumberGenerator _rng = new();
	private Rect2[] _obstacles;
	private Vector2 _goal = new(220, 500);
	private TeamState _teamState = TeamState.Move;
	private string _status = "右键下达小队前进点。";
	private float _extractProgress;
	private int _lootValue;
	private int _aliveCount;
	private bool _extracted;
	private bool _hasManualGoal = true;
	private bool _lastLeftPressed;
	private Loot _activeContainer;

	public override void _Ready()
	{
		_rng.Randomize();
		_obstacles =
		[
			new Rect2(new Vector2(250, 160), new Vector2(150, 120)),
			new Rect2(new Vector2(520, 135), new Vector2(115, 210)),
			new Rect2(new Vector2(775, 185), new Vector2(135, 110)),
			new Rect2(new Vector2(340, 420), new Vector2(180, 85)),
			new Rect2(new Vector2(690, 430), new Vector2(170, 95)),
		];
		_squad.Add(new Unit { Name = "山隼", Position = new Vector2(170, 520), Offset = new Vector2(0, 0), Hp = 10, MaxHp = 10, Ammo = 8, Medkits = 0 });
		_squad.Add(new Unit { Name = "猎隼", Position = new Vector2(135, 550), Offset = new Vector2(-34, 24), Ammo = 9, Medkits = 0 });
		_squad.Add(new Unit { Name = "白芷", Position = new Vector2(205, 550), Offset = new Vector2(34, 24), Hp = 7, MaxHp = 7, Ammo = 6, Medkits = 2 });
		_loot.Add(new Loot { Label = "办公室暗箱", Position = new Vector2(175, 165), Value = 18 });
		_loot.Add(new Loot { Label = "工具箱", Position = new Vector2(585, 410), Value = 26 });
		_loot.Add(new Loot { Label = "钥匙房", Position = new Vector2(920, 150), Value = 42 });
		_loot.Add(new Loot { Label = "货运背包", Position = new Vector2(845, 535), Value = 16 });
		_loot.Clear();
		_loot.Add(CreateContainer("办公室暗箱", new Vector2(175, 165), new Vector2I(5, 4), 4));
		_loot.Add(CreateContainer("工具箱", new Vector2(585, 410), new Vector2I(6, 5), 5));
		_loot.Add(CreateContainer("钥匙房", new Vector2(920, 150), new Vector2I(7, 5), 6));
		_loot.Add(CreateContainer("货运背包", new Vector2(845, 535), new Vector2I(5, 5), 4));
		_enemies.Add(new Enemy { Position = new Vector2(470, 185), Patrol = [new Vector2(450, 150), new Vector2(700, 150), new Vector2(700, 350), new Vector2(450, 350)] });
		_enemies.Add(new Enemy { Position = new Vector2(840, 370), Patrol = [new Vector2(820, 360), new Vector2(920, 490), new Vector2(730, 500)] });
		_enemies.Add(new Enemy { Position = new Vector2(240, 420), Patrol = [new Vector2(170, 430), new Vector2(270, 555), new Vector2(400, 555)] });
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		bool leftPressed = Input.IsMouseButtonPressed(MouseButton.Left);
		if (leftPressed && !_lastLeftPressed)
		{
			HandleHudClick(GetGlobalMousePosition());
		}
		_lastLeftPressed = leftPressed;

		if (Input.IsMouseButtonPressed(MouseButton.Right))
		{
			_goal = ClampInside(GetGlobalMousePosition(), 18f);
			_hasManualGoal = true;
			_status = "已调整小队前进点。";
		}
		if (Input.IsKeyPressed(Key.F))
		{
			_goal = _extract.GetCenter();
			_hasManualGoal = true;
			_teamState = TeamState.Extract;
		}
		if (!_extracted)
		{
			UpdateState();
			UpdateSquad(dt);
			UpdateEnemies(dt);
			UpdateLoot(dt);
			UpdateExtract(dt);
		}
		for (int i = _fx.Count - 1; i >= 0; i--) { _fx[i].TimeLeft -= dt; if (_fx[i].TimeLeft <= 0f) _fx.RemoveAt(i); }
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(_map, new Color(0.08f, 0.09f, 0.11f), true);
		DrawRect(_map, new Color(0.22f, 0.24f, 0.28f), false, 2f);

		foreach (Rect2 obstacle in _obstacles)
		{
			DrawRect(obstacle, new Color(0.26f, 0.29f, 0.33f), true);
			DrawRect(obstacle, new Color(0.8f, 0.84f, 0.9f, 0.55f), false, 2f);
		}

		DrawRect(_extract, new Color(0.16f, 0.34f, 0.2f, 0.85f), true);
		DrawRect(_extract, new Color(0.56f, 0.95f, 0.62f), false, 2f);
		DrawString(ThemeDB.FallbackFont, _extract.Position + new Vector2(14f, 24f), "撤离区", HorizontalAlignment.Left, -1f, 14, Colors.White);

		DrawGoalMarker();
		DrawLoot();
		DrawEnemies();
		DrawShots();
		DrawSquad();
		DrawHud();
	}

	private void UpdateState()
	{
		_aliveCount = 0;
		bool seesEnemy = false;
		foreach (Unit u in _squad)
		{
			if (!u.Alive) continue;
			_aliveCount++;
			foreach (Loot l in _loot) if (!l.Taken && CanSee(u.Position, l.Position, u.Vision)) l.Seen = true;
			if (FindVisibleEnemy(u) != null) seesEnemy = true;
		}
		_activeContainer = GetContainerNearSquad();
		if (seesEnemy) { _teamState = TeamState.Fight; _status = "发现敌情，小队开始交火。"; return; }

		bool commandActive = _hasManualGoal && LeaderPos().DistanceTo(_goal) > CommandReachRadius;
		if (_teamState != TeamState.Extract && (_lootValue >= 50 || _aliveCount < 3 || TotalAmmo() <= 4))
		{
			_teamState = TeamState.Extract;
			_goal = _extract.GetCenter();
			_hasManualGoal = true;
			_status = "物资或状态到线，准备撤离。";
			return;
		}

		if (_teamState == TeamState.Extract)
		{
			return;
		}

		if (commandActive)
		{
			_teamState = TeamState.Move;
			_status = "正在按命令推进。";
			return;
		}

		_hasManualGoal = false;
		Loot nextLoot = BestLoot(LeaderPos(), ManualLootRadius);
		if (nextLoot != null) { _teamState = TeamState.Loot; _status = $"小队正在检查 {nextLoot.Label}。"; return; }
		if (_teamState != TeamState.Extract) _teamState = TeamState.Move;
	}

	private void UpdateSquad(float dt)
	{
		Vector2 anchor = LeaderPos();
		Vector2 forward = (_goal - anchor).LengthSquared() > 9f ? (_goal - anchor).Normalized() : Vector2.Right;
		foreach (Unit u in _squad)
		{
			if (!u.Alive) continue;
			u.Cooldown = Mathf.Max(0f, u.Cooldown - dt);
			u.IsRunning = false;
			u.IsMoving = false;
			if (_squad.Count > 2 && ReferenceEquals(u, _squad[2])) TryHeal(u);
			if (_teamState == TeamState.Fight) UpdateFight(u, dt);
			else if (_teamState == TeamState.Loot) UpdateLootMove(u, dt, forward);
			else UpdateFollow(u, dt, forward, _teamState == TeamState.Extract ? 1.1f : 1f);
			UpdateUnitStamina(u, dt);
		}
		ResolveSquadSeparation();
	}

	private void UpdateFollow(Unit u, float dt, Vector2 forward, float factor)
	{
		Vector2 right = new(-forward.Y, forward.X);
		Vector2 slot = _goal + right * u.Offset.X + forward * u.Offset.Y;
		bool preferRun = _teamState != TeamState.Fight && u.Position.DistanceTo(slot) > 75f;
		MoveUnit(u, slot, dt, factor, preferRun);
	}

	private void UpdateLootMove(Unit u, float dt, Vector2 forward)
	{
		Loot l = BestLoot(LeaderPos(), ManualLootRadius);
		if (l == null) { UpdateFollow(u, dt, forward, 1f); return; }
		if (!u.SearchEnabled)
		{
			UpdateFollow(u, dt, forward, 0.9f);
			return;
		}

		Vector2 right = new(-forward.Y, forward.X);
		Vector2 approach = l.Position + right * u.Offset.X * 0.65f + forward * u.Offset.Y * 0.35f;
		if (u.Position.DistanceTo(l.Position) > LootInteractRadius)
		{
			bool preferRun = u.Position.DistanceTo(approach) > 95f;
			MoveUnit(u, approach, dt, 0.9f, preferRun);
		}
	}

	private void UpdateFight(Unit u, float dt)
	{
		Enemy e = FindVisibleEnemy(u);
		if (e == null) { UpdateFollow(u, dt, (_goal - u.Position).Normalized(), 0.9f); return; }
		Vector2 toEnemy = e.Position - u.Position;
		if (toEnemy.LengthSquared() > 1f) u.Facing = toEnemy.Normalized();
		float distance = toEnemy.Length();
		Vector2 cover = FindCover(u, e);
		if (distance > u.Range * 0.85f) MoveUnit(u, e.Position, dt, 1f, distance > u.Range * 1.2f);
		else if (cover != Vector2.Zero) MoveUnit(u, cover, dt, 0.9f, u.Position.DistanceTo(cover) > 80f);
		else if (distance < 100f) MoveUnit(u, u.Position - u.Facing * 55f, dt, 0.85f, false);
		if (u.Cooldown <= 0f && u.Ammo > 0 && CanSee(u.Position, e.Position, u.Range))
		{
			u.Ammo -= 1;
			bool isMarksman = _squad.Count > 1 && ReferenceEquals(u, _squad[1]);
			u.Cooldown = isMarksman ? 0.28f : 0.4f;
			ShotRoll roll = RollShot(
				u.Position,
				e.Position,
				distance,
				u.Range,
				u.Accuracy,
				u.HeadshotChance,
				u.IsMoving,
				u.IsRunning,
				e.IsMoving,
				false);
			if (roll.Hit)
			{
				int baseDamage = isMarksman ? 2 : 1;
				int damage = baseDamage + (roll.Headshot ? 1 : 0);
				e.Hp -= damage;
				Color shotColor = roll.Headshot ? new Color(1f, 0.84f, 0.2f) : new Color(0.4f, 0.95f, 1f);
				_fx.Add(new Fx { From = u.Position, To = e.Position, Color = shotColor, TimeLeft = 0.12f });
				if (roll.Headshot) _status = $"{u.Name} HEADSHOT!";
			}
			else
			{
				_fx.Add(new Fx { From = u.Position, To = roll.TraceEnd, Color = new Color(0.3f, 0.75f, 1f, 0.85f), TimeLeft = 0.1f });
			}
			AlertEnemies(u.Position);
		}
	}

	private void UpdateEnemies(float dt)
	{
		foreach (Enemy e in _enemies)
		{
			if (!e.Alive) continue;
			e.Cooldown = Mathf.Max(0f, e.Cooldown - dt);
			e.IsMoving = false;
			Unit target = FindSeenSquad(e);
			if (target != null) { e.State = EnemyState.Engage; e.LastSeen = target.Position; e.StateTimer = 1.8f; }
			else if (e.State != EnemyState.Patrol) { e.StateTimer -= dt; e.State = e.StateTimer <= 0f ? EnemyState.Patrol : EnemyState.Investigate; }
			if (e.State == EnemyState.Patrol) PatrolEnemy(e, dt);
			else if (e.State == EnemyState.Investigate) MoveEnemy(e, e.LastSeen, dt, 0.95f);
			else EngageEnemy(e, dt);
		}
	}

	private void PatrolEnemy(Enemy e, float dt)
	{
		if (e.Patrol.Length == 0) return;
		Vector2 target = e.Patrol[e.PatrolIndex];
		if (e.Position.DistanceTo(target) <= 14f) { e.PatrolIndex = (e.PatrolIndex + 1) % e.Patrol.Length; target = e.Patrol[e.PatrolIndex]; }
		MoveEnemy(e, target, dt, 0.82f);
	}

	private void EngageEnemy(Enemy e, float dt)
	{
		Unit target = FindSeenSquad(e) ?? NearestAlive(e.Position);
		if (target == null) return;
		e.LastSeen = target.Position;
		float d = e.Position.DistanceTo(target.Position);
		Vector2 dir = (target.Position - e.Position).Normalized();
		e.Facing = dir;
		if (d > 165f) MoveEnemy(e, target.Position, dt, 1f);
		else if (d < 105f) MoveEnemy(e, e.Position - dir * 60f, dt, 0.85f);
		if (e.Cooldown <= 0f && d <= 210f)
		{
			if (TryObstacleHit(e.Position, target.Position, out Vector2 hit2, out _))
			{
				_fx.Add(new Fx { From = e.Position, To = hit2, Color = new Color(0.85f, 0.34f, 0.34f), TimeLeft = 0.09f });
			}
			else
			{
				ShotRoll roll = RollShot(
					e.Position,
					target.Position,
					d,
					210f,
					e.Accuracy,
					e.HeadshotChance,
					e.IsMoving,
					false,
					target.IsMoving,
					target.IsRunning);
				if (roll.Hit)
				{
					int damage = roll.Headshot ? 2 : 1;
					target.Hp = Mathf.Max(0, target.Hp - damage);
					Color shotColor = roll.Headshot ? new Color(1f, 0.75f, 0.18f) : new Color(1f, 0.42f, 0.42f);
					_fx.Add(new Fx { From = e.Position, To = target.Position, Color = shotColor, TimeLeft = 0.12f });
					if (!target.Alive) _status = $"{target.Name} is down.";
					else if (roll.Headshot) _status = $"{target.Name} was headshotted!";
					else _status = $"{target.Name} is under fire.";
				}
				else
				{
					_fx.Add(new Fx { From = e.Position, To = roll.TraceEnd, Color = new Color(0.95f, 0.32f, 0.32f, 0.85f), TimeLeft = 0.1f });
				}
			}
			e.Cooldown = 0.72f;
			return;
		}
		if (e.Cooldown <= 0f && d <= 210f)
		{
			if (TryObstacleHit(e.Position, target.Position, out Vector2 hit, out _)) _fx.Add(new Fx { From = e.Position, To = hit, Color = new Color(0.85f, 0.34f, 0.34f), TimeLeft = 0.09f });
			else { target.Hp = Mathf.Max(0, target.Hp - 1); _fx.Add(new Fx { From = e.Position, To = target.Position, Color = new Color(1f, 0.42f, 0.42f), TimeLeft = 0.12f }); _status = target.Alive ? $"{target.Name} 遭到压制射击。" : $"{target.Name} 已失去战斗能力。"; }
			e.Cooldown = 0.72f;
		}
	}
	private void UpdateLoot(float dt)
	{
		foreach (Loot container in _loot)
		{
			if (container.Taken)
			{
				continue;
			}

			foreach (LootItem item in container.Items)
			{
				if (item.Revealed)
				{
					item.Searcher = null;
					continue;
				}

				if (item.Searcher != null && (!item.Searcher.Alive || !item.Searcher.SearchEnabled || item.Searcher.Position.DistanceTo(container.Position) > LootInteractRadius))
				{
					item.Searcher = null;
					item.SearchProgress = 0f;
				}
			}

			AssignContainerSearchers(container);

			foreach (LootItem item in container.Items)
			{
				if (item.Revealed || item.Searcher == null)
				{
					continue;
				}

				item.SearchProgress += dt;
				if (item.SearchProgress >= item.SearchTime)
				{
					item.Revealed = true;
					item.Searcher = null;
					_lootValue += item.Value;
					_status = $"已搜出 {item.Label}。";
				}
			}

			container.Taken = IsContainerFinished(container);
		}
	}

	private void UpdateExtract(float dt)
	{
		if (_teamState != TeamState.Extract) { _extractProgress = 0f; return; }
		foreach (Unit u in _squad) if (u.Alive && !_extract.HasPoint(u.Position)) { _extractProgress = 0f; return; }
		if (_lootValue <= 0) { _extractProgress = 0f; return; }
		_extractProgress += dt;
		if (_extractProgress >= 1.4f) { _extracted = true; _status = $"撤离成功，带出价值 {_lootValue}，生还 {_aliveCount}/3。"; }
	}

	private void TryHeal(Unit medic)
	{
		if (medic.Medkits <= 0) return;
		Unit best = null; float bestRatio = 1f;
		foreach (Unit u in _squad)
		{
			if (!u.Alive || ReferenceEquals(u, medic) || medic.Position.DistanceTo(u.Position) > 42f) continue;
			float ratio = (float)u.Hp / u.MaxHp;
			if (ratio < 0.55f && ratio < bestRatio) { bestRatio = ratio; best = u; }
		}
		if (best == null) return;
		best.Hp = Mathf.Min(best.MaxHp, best.Hp + 3); medic.Medkits -= 1; _status = $"白芷为 {best.Name} 完成包扎。";
	}

	private Enemy FindVisibleEnemy(Unit u)
	{
		Enemy best = null; float bestD = float.MaxValue;
		foreach (Enemy e in _enemies)
		{
			if (!e.Alive || !CanSee(u.Position, e.Position, u.Vision)) continue;
			float d = u.Position.DistanceTo(e.Position);
			if (d < bestD) { bestD = d; best = e; }
		}
		return best;
	}

	private Unit FindSeenSquad(Enemy e)
	{
		Unit best = null; float bestD = float.MaxValue;
		foreach (Unit u in _squad)
		{
			if (!u.Alive || !CanSee(e.Position, u.Position, 230f)) continue;
			float d = e.Position.DistanceTo(u.Position);
			if (d < bestD) { bestD = d; best = u; }
		}
		return best;
	}

	private Unit NearestAlive(Vector2 point)
	{
		Unit best = null; float bestD = float.MaxValue;
		foreach (Unit u in _squad) { if (!u.Alive) continue; float d = u.Position.DistanceTo(point); if (d < bestD) { bestD = d; best = u; } }
		return best;
	}

	private Loot BestLoot(Vector2 center, float maxRadius)
	{
		Loot best = null; float bestScore = float.MinValue;
		foreach (Loot l in _loot)
		{
			if (l.Taken || !l.Seen || IsContainerFinished(l)) continue;
			float distance = center.DistanceTo(l.Position);
			if (distance > maxRadius) continue;
			float score = CountHiddenItems(l) * 12f + TotalContainerValue(l) * 0.25f - distance * 0.08f;
			if (score > bestScore) { bestScore = score; best = l; }
		}
		return best;
	}

	private Loot CreateContainer(string label, Vector2 position, Vector2I gridSize, int itemCount)
	{
		Loot container = new() { Label = label, Position = position, GridSize = gridSize };
		for (int i = 0; i < itemCount; i++)
		{
			LootItem item = CreateRandomItem();
			if (!TryPlaceItem(container, item))
			{
				break;
			}
		}

		return container;
	}

	private LootItem CreateRandomItem()
	{
		ItemRarity rarity = RollRarity();
		Vector2I size = rarity switch
		{
			ItemRarity.White => _rng.Randf() < 0.6f ? new Vector2I(1, 1) : new Vector2I(1, 2),
			ItemRarity.Green => _rng.Randf() < 0.5f ? new Vector2I(1, 2) : new Vector2I(2, 1),
			ItemRarity.Blue => _rng.Randf() < 0.5f ? new Vector2I(2, 2) : new Vector2I(1, 3),
			ItemRarity.Purple => new Vector2I(2, 2),
			ItemRarity.Gold => new Vector2I(2, 3),
			_ => new Vector2I(3, 2),
		};

		return new LootItem
		{
			Label = GetItemName(rarity),
			Rarity = rarity,
			Size = size,
			Value = GetItemValue(rarity),
			SearchTime = GetSearchTime(rarity),
		};
	}

	private bool TryPlaceItem(Loot container, LootItem item)
	{
		for (int y = 0; y <= container.GridSize.Y - item.Size.Y; y++)
		{
			for (int x = 0; x <= container.GridSize.X - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsAreaFree(container, cell, item.Size))
				{
					continue;
				}

				item.Cell = cell;
				container.Items.Add(item);
				return true;
			}
		}

		return false;
	}

	private bool IsAreaFree(Loot container, Vector2I cell, Vector2I size)
	{
		foreach (LootItem existing in container.Items)
		{
			bool overlapX = cell.X < existing.Cell.X + existing.Size.X && cell.X + size.X > existing.Cell.X;
			bool overlapY = cell.Y < existing.Cell.Y + existing.Size.Y && cell.Y + size.Y > existing.Cell.Y;
			if (overlapX && overlapY)
			{
				return false;
			}
		}

		return true;
	}

	private void AssignContainerSearchers(Loot container)
	{
		foreach (Unit unit in _squad)
		{
			if (!unit.Alive || !unit.SearchEnabled || unit.Position.DistanceTo(container.Position) > LootInteractRadius)
			{
				continue;
			}

			bool alreadyAssigned = false;
			foreach (LootItem item in container.Items)
			{
				if (ReferenceEquals(item.Searcher, unit) && !item.Revealed)
				{
					alreadyAssigned = true;
					break;
				}
			}

			if (alreadyAssigned)
			{
				continue;
			}

			LootItem next = GetNextSearchableItem(container);
			if (next == null)
			{
				return;
			}

			next.Searcher = unit;
			next.SearchProgress = 0f;
		}
	}

	private LootItem GetNextSearchableItem(Loot container)
	{
		LootItem best = null;
		int bestIndex = int.MaxValue;
		foreach (LootItem item in container.Items)
		{
			if (item.Revealed || item.Searcher != null)
			{
				continue;
			}

			int index = item.Cell.Y * 100 + item.Cell.X;
			if (index < bestIndex)
			{
				bestIndex = index;
				best = item;
			}
		}

		return best;
	}

	private Loot GetContainerNearSquad()
	{
		Loot best = null;
		float bestDistance = float.MaxValue;
		foreach (Loot container in _loot)
		{
			if (!container.Seen || container.Taken)
			{
				continue;
			}

			foreach (Unit unit in _squad)
			{
				if (!unit.Alive)
				{
					continue;
				}

				float distance = unit.Position.DistanceTo(container.Position);
				if (distance <= LootInteractRadius && distance < bestDistance)
				{
					bestDistance = distance;
					best = container;
				}
			}
		}

		return best;
	}

	private bool IsContainerFinished(Loot container)
	{
		foreach (LootItem item in container.Items)
		{
			if (!item.Revealed)
			{
				return false;
			}
		}

		return true;
	}

	private int CountHiddenItems(Loot container)
	{
		int count = 0;
		foreach (LootItem item in container.Items)
		{
			if (!item.Revealed)
			{
				count++;
			}
		}

		return count;
	}

	private int TotalContainerValue(Loot container)
	{
		int total = 0;
		foreach (LootItem item in container.Items)
		{
			if (!item.Revealed)
			{
				total += item.Value;
			}
		}

		return total;
	}

	private ItemRarity RollRarity()
	{
		float roll = _rng.Randf();
		if (roll < 0.38f) return ItemRarity.White;
		if (roll < 0.64f) return ItemRarity.Green;
		if (roll < 0.82f) return ItemRarity.Blue;
		if (roll < 0.93f) return ItemRarity.Purple;
		if (roll < 0.985f) return ItemRarity.Gold;
		return ItemRarity.Red;
	}

	private string GetItemName(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => "杂物",
		ItemRarity.Green => "常用物资",
		ItemRarity.Blue => "军用配件",
		ItemRarity.Purple => "稀有电子件",
		ItemRarity.Gold => "高价值情报",
		ItemRarity.Red => "红色珍品",
		_ => "未知物品",
	};

	private int GetItemValue(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 6,
		ItemRarity.Green => 10,
		ItemRarity.Blue => 18,
		ItemRarity.Purple => 28,
		ItemRarity.Gold => 42,
		ItemRarity.Red => 65,
		_ => 0,
	};

	private float GetSearchTime(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 0.8f,
		ItemRarity.Green => 1.15f,
		ItemRarity.Blue => 1.6f,
		ItemRarity.Purple => 2.15f,
		ItemRarity.Gold => 2.9f,
		ItemRarity.Red => 3.8f,
		_ => 1f,
	};

	private Color GetRarityColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => new Color(0.75f, 0.77f, 0.8f),
		ItemRarity.Green => new Color(0.45f, 0.88f, 0.45f),
		ItemRarity.Blue => new Color(0.35f, 0.68f, 1f),
		ItemRarity.Purple => new Color(0.75f, 0.42f, 1f),
		ItemRarity.Gold => new Color(1f, 0.8f, 0.25f),
		ItemRarity.Red => new Color(1f, 0.34f, 0.34f),
		_ => Colors.White,
	};

	private void HandleHudClick(Vector2 click)
	{
		for (int i = 0; i < _squad.Count && i < _searchToggleRects.Length; i++)
		{
			if (_searchToggleRects[i].HasPoint(click))
			{
				_squad[i].SearchEnabled = !_squad[i].SearchEnabled;
				_status = _squad[i].SearchEnabled ? $"{_squad[i].Name} 允许参与搜索。" : $"{_squad[i].Name} 暂停参与搜索。";
				return;
			}
		}

		for (int i = 0; i < _squad.Count && i < _runToggleRects.Length; i++)
		{
			if (_runToggleRects[i].HasPoint(click))
			{
				_squad[i].RunEnabled = !_squad[i].RunEnabled;
				_squad[i].IsRunning = false;
				_status = _squad[i].RunEnabled ? $"{_squad[i].Name} run enabled." : $"{_squad[i].Name} run disabled.";
				return;
			}
		}
	}

	private void AlertEnemies(Vector2 noise)
	{
		foreach (Enemy e in _enemies) if (e.Alive && e.Position.DistanceTo(noise) <= 280f) { e.State = EnemyState.Investigate; e.LastSeen = noise; e.StateTimer = 2.2f; }
	}

	private Vector2 LeaderPos()
	{
		foreach (Unit u in _squad) if (u.Alive) return u.Position;
		return _squad.Count > 0 ? _squad[0].Position : _goal;
	}

	private Vector2 FindCover(Unit u, Enemy e)
	{
		float best = float.MaxValue; Vector2 result = Vector2.Zero;
		foreach (Rect2 o in _obstacles)
		{
			if (!SegmentRect(e.Position, u.Position, o)) continue;
			Vector2 away = (o.GetCenter() - e.Position).Normalized();
			Vector2 candidate = ClampInside(o.GetCenter() + away * (Mathf.Max(o.Size.X, o.Size.Y) * 0.6f + u.Radius + 18f), u.Radius);
			float score = u.Position.DistanceSquaredTo(candidate);
			if (score < best) { best = score; result = candidate; }
		}
		return result;
	}

	private void MoveUnit(Unit u, Vector2 target, float dt, float factor, bool preferRun = false)
	{
		Vector2 moveTarget = ResolveSteerTarget(u.Position, target, u.Radius);
		Vector2 dir = moveTarget - u.Position;
		if (dir.LengthSquared() <= 4f)
		{
			u.IsRunning = false;
			return;
		}

		Vector2 direction = dir.Normalized();
		bool canRunByFacing = IsRunDirectionValid(u.Facing, direction);
		bool hasStaminaForRun = u.Stamina > 0.1f;
		bool canRun = preferRun && u.RunEnabled && hasStaminaForRun && canRunByFacing && dir.Length() > 65f;
		float speedMultiplier = canRun ? u.RunSpeedMultiplier : 1f;
		u.IsRunning = canRun;
		u.Facing = direction;
		u.Position = ResolveMove(u.Position, u.Radius, u.Position + direction * u.Speed * factor * speedMultiplier * dt);
		u.IsMoving = true;
	}

	private static bool IsRunDirectionValid(Vector2 facing, Vector2 moveDirection)
	{
		if (moveDirection.LengthSquared() <= 0.0001f)
		{
			return false;
		}

		Vector2 currentFacing = facing.LengthSquared() <= 0.0001f ? Vector2.Right : facing.Normalized();
		float dot = Mathf.Clamp(currentFacing.Dot(moveDirection), -1f, 1f);
		float angle = Mathf.Acos(dot);
		return angle <= Mathf.DegToRad(45f);
	}

	private static void UpdateUnitStamina(Unit u, float dt)
	{
		if (u.IsRunning)
		{
			u.Stamina = Mathf.Max(0f, u.Stamina - u.StaminaDrain * dt);
			if (u.Stamina <= 0.01f)
			{
				u.IsRunning = false;
			}

			return;
		}

		float regenCap = Mathf.Max(0f, u.StaminaDrain * 0.5f);
		float effectiveRegen = Mathf.Min(u.StaminaRegen, regenCap);
		u.Stamina = Mathf.Min(u.MaxStamina, u.Stamina + effectiveRegen * dt);
	}

	private ShotRoll RollShot(
		Vector2 from,
		Vector2 target,
		float distance,
		float effectiveRange,
		float baseAccuracy,
		float baseHeadshotChance,
		bool shooterMoving,
		bool shooterRunning,
		bool targetMoving,
		bool targetRunning)
	{
		float safeRange = Mathf.Max(1f, effectiveRange);
		float distRatio = distance / safeRange;
		float hitChance = baseAccuracy * Mathf.Lerp(1.05f, 0.58f, Mathf.Clamp(distRatio, 0f, 1f));
		if (distRatio > 1f)
		{
			hitChance -= (distRatio - 1f) * 0.32f;
		}

		if (shooterRunning) hitChance -= 0.22f;
		else if (shooterMoving) hitChance -= 0.12f;
		if (targetRunning) hitChance -= 0.18f;
		else if (targetMoving) hitChance -= 0.1f;

		hitChance -= GetCoverPenalty(target);
		hitChance = Mathf.Clamp(hitChance, 0.08f, 0.97f);
		bool hit = _rng.Randf() <= hitChance;
		if (!hit)
		{
			Vector2 toTarget = target - from;
			Vector2 dir = toTarget.LengthSquared() <= 0.001f ? Vector2.Right : toTarget.Normalized();
			Vector2 perp = new Vector2(-dir.Y, dir.X);
			float spread = Mathf.Lerp(14f, 58f, Mathf.Clamp(distRatio, 0f, 1.2f));
			float lateral = _rng.RandfRange(-spread, spread);
			float forward = _rng.RandfRange(-spread * 0.2f, spread * 0.55f);
			Vector2 missPoint = ClampInside(target + perp * lateral + dir * forward, 2f);
			return new ShotRoll(false, false, missPoint);
		}

		float headshotChance = baseHeadshotChance * Mathf.Lerp(1.15f, 0.55f, Mathf.Clamp(distRatio, 0f, 1f));
		if (shooterRunning) headshotChance *= 0.5f;
		else if (shooterMoving) headshotChance *= 0.78f;
		if (targetRunning) headshotChance *= 0.55f;
		else if (targetMoving) headshotChance *= 0.75f;

		headshotChance = Mathf.Clamp(headshotChance, 0.01f, 0.45f);
		bool headshot = _rng.Randf() <= headshotChance;
		return new ShotRoll(true, headshot, target);
	}

	private float GetCoverPenalty(Vector2 targetPosition)
	{
		float nearest = float.MaxValue;
		foreach (Rect2 obstacle in _obstacles)
		{
			float x = Mathf.Clamp(targetPosition.X, obstacle.Position.X, obstacle.End.X);
			float y = Mathf.Clamp(targetPosition.Y, obstacle.Position.Y, obstacle.End.Y);
			float distance = targetPosition.DistanceTo(new Vector2(x, y));
			if (distance < nearest) nearest = distance;
		}

		if (nearest <= 8f) return 0.18f;
		if (nearest <= 20f) return 0.11f;
		if (nearest <= 34f) return 0.05f;
		return 0f;
	}

	private void MoveEnemy(Enemy e, Vector2 target, float dt, float factor)
	{
		Vector2 moveTarget = ResolveSteerTarget(e.Position, target, 14f);
		Vector2 dir = moveTarget - e.Position;
		if (dir.LengthSquared() <= 4f) return;
		e.Facing = dir.Normalized();
		e.Position = ResolveMove(e.Position, 14f, e.Position + e.Facing * 118f * factor * dt);
		e.IsMoving = true;
		ResolveEnemySeparation(e);
	}

	private bool CanSee(Vector2 from, Vector2 to, float range) => from.DistanceTo(to) <= range && !TryObstacleHit(from, to, out _, out _);

	private Vector2 ResolveSteerTarget(Vector2 from, Vector2 target, float radius)
	{
		if (!TryObstacleHit(from, target, out _, out _))
		{
			return target;
		}

		Vector2I start = WorldToCell(from);
		Vector2I goal = FindNearestFreeCell(WorldToCell(target), radius);
		Vector2I best = start;
		float bestDist = CellToWorld(start).DistanceSquaredTo(target);

		int width = Mathf.CeilToInt(_map.Size.X / NavCellSize);
		int height = Mathf.CeilToInt(_map.Size.Y / NavCellSize);
		bool[,] visited = new bool[width, height];
		Vector2I[,] prev = new Vector2I[width, height];
		Queue<Vector2I> queue = new();

		queue.Enqueue(start);
		visited[start.X, start.Y] = true;
		prev[start.X, start.Y] = new Vector2I(-1, -1);

		Vector2I[] dirs =
		{
			new Vector2I(1, 0),
			new Vector2I(-1, 0),
			new Vector2I(0, 1),
			new Vector2I(0, -1),
		};

		while (queue.Count > 0)
		{
			Vector2I current = queue.Dequeue();
			float currentDist = CellToWorld(current).DistanceSquaredTo(target);
			if (currentDist < bestDist)
			{
				bestDist = currentDist;
				best = current;
			}

			if (current == goal)
			{
				best = current;
				break;
			}

			foreach (Vector2I dir in dirs)
			{
				Vector2I next = current + dir;
				if (next.X < 0 || next.Y < 0 || next.X >= width || next.Y >= height || visited[next.X, next.Y] || IsCellBlocked(next, radius))
				{
					continue;
				}

				visited[next.X, next.Y] = true;
				prev[next.X, next.Y] = current;
				queue.Enqueue(next);
			}
		}

		Vector2I step = best;
		while (prev[step.X, step.Y].X != -1 && prev[step.X, step.Y] != start)
		{
			step = prev[step.X, step.Y];
		}

		Vector2 waypoint = CellToWorld(step);
		return ClampInside(waypoint, radius);
	}

	private Vector2I FindNearestFreeCell(Vector2I desired, float radius)
	{
		int width = Mathf.CeilToInt(_map.Size.X / NavCellSize);
		int height = Mathf.CeilToInt(_map.Size.Y / NavCellSize);
		desired = new Vector2I(Mathf.Clamp(desired.X, 0, width - 1), Mathf.Clamp(desired.Y, 0, height - 1));
		if (!IsCellBlocked(desired, radius))
		{
			return desired;
		}

		for (int ring = 1; ring <= 6; ring++)
		{
			for (int y = -ring; y <= ring; y++)
			{
				for (int x = -ring; x <= ring; x++)
				{
					if (Mathf.Abs(x) != ring && Mathf.Abs(y) != ring)
					{
						continue;
					}

					Vector2I cell = new(desired.X + x, desired.Y + y);
					if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height || IsCellBlocked(cell, radius))
					{
						continue;
					}

					return cell;
				}
			}
		}

		return desired;
	}

	private bool IsCellBlocked(Vector2I cell, float radius)
	{
		Vector2 center = CellToWorld(cell);
		foreach (Rect2 obstacle in _obstacles)
		{
			if (obstacle.Grow(radius + 6f).HasPoint(center))
			{
				return true;
			}
		}

		return false;
	}

	private Vector2I WorldToCell(Vector2 point)
	{
		Vector2 local = point - _map.Position;
		return new Vector2I(
			Mathf.Clamp(Mathf.FloorToInt(local.X / NavCellSize), 0, Mathf.CeilToInt(_map.Size.X / NavCellSize) - 1),
			Mathf.Clamp(Mathf.FloorToInt(local.Y / NavCellSize), 0, Mathf.CeilToInt(_map.Size.Y / NavCellSize) - 1));
	}

	private Vector2 CellToWorld(Vector2I cell)
	{
		return _map.Position + new Vector2((cell.X + 0.5f) * NavCellSize, (cell.Y + 0.5f) * NavCellSize);
	}
	private void ResolveSquadSeparation()
	{
		for (int i = 0; i < _squad.Count; i++)
		{
			if (!_squad[i].Alive) continue;
			for (int j = i + 1; j < _squad.Count; j++)
			{
				if (!_squad[j].Alive) continue;
				Vector2 off = _squad[i].Position - _squad[j].Position; float d = off.Length(); float min = _squad[i].Radius + _squad[j].Radius + 6f;
				if (d >= min) continue;
				Vector2 push = d > 0.001f ? off / d : Vector2.Right;
				_squad[i].Position = ResolveMove(_squad[i].Position, _squad[i].Radius, _squad[i].Position + push * ((min - d) * 0.55f));
				_squad[j].Position = ResolveMove(_squad[j].Position, _squad[j].Radius, _squad[j].Position - push * ((min - d) * 0.55f));
			}
		}
	}

	private void ResolveEnemySeparation(Enemy e)
	{
		foreach (Enemy other in _enemies)
		{
			if (ReferenceEquals(e, other) || !other.Alive) continue;
			Vector2 off = e.Position - other.Position; float d = off.Length(); if (d >= 30f) continue;
			Vector2 push = d > 0.001f ? off / d : Vector2.Right; e.Position = ResolveMove(e.Position, 14f, other.Position + push * 30f);
		}
		foreach (Unit u in _squad)
		{
			if (!u.Alive) continue;
			Vector2 off = e.Position - u.Position; float d = off.Length(); float min = u.Radius + 16f; if (d >= min) continue;
			Vector2 push = d > 0.001f ? off / d : Vector2.Right; e.Position = ResolveMove(e.Position, 14f, u.Position + push * min);
		}
	}

	private int TotalAmmo() { int a = 0; foreach (Unit u in _squad) if (u.Alive) a += u.Ammo; return a; }
	private float AverageSquadHpRatio() { float s = 0f; int c = 0; foreach (Unit u in _squad) if (u.Alive) { s += (float)u.Hp / u.MaxHp; c++; } return c == 0 ? 0f : s / c; }
	private string GetStateLabel() => _teamState switch
	{
		TeamState.Move => "机动",
		TeamState.Fight => "交火",
		TeamState.Loot => "搜刮",
		TeamState.Extract => "撤离",
		_ => "待命",
	};

	private void DrawGoalMarker() { DrawCircle(_goal, 10f, new Color(0.45f, 0.88f, 1f, 0.28f)); DrawArc(_goal, 18f, 0f, Mathf.Tau, 20, new Color(0.45f, 0.88f, 1f), 2f); }
	private void DrawLoot()
	{
		foreach (Loot container in _loot)
		{
			if (container.Taken || !container.Seen) continue;
			int active = 0;
			foreach (LootItem item in container.Items) if (!item.Revealed && item.Searcher != null) active++;
			Color c = active > 0 ? new Color(1f, 0.92f, 0.36f) : new Color(0.82f, 0.7f, 0.24f);
			DrawCircle(container.Position, 10f, c);
			DrawArc(container.Position, LootInteractRadius, 0f, Mathf.Tau, 30, new Color(c.R, c.G, c.B, 0.28f), 2f);
			DrawString(ThemeDB.FallbackFont, container.Position + new Vector2(-28f, -24f), container.Label, HorizontalAlignment.Left, -1f, 12, Colors.White);
		}
	}
	private void DrawEnemies() { foreach (Enemy e in _enemies) { bool vis = false; foreach (Unit u in _squad) if (u.Alive && CanSee(u.Position, e.Position, u.Vision)) { vis = true; break; } if (!e.Alive || !vis) continue; Vector2 r = new(-e.Facing.Y, e.Facing.X); Vector2[] h = [e.Position + e.Facing * 16f, e.Position - e.Facing * 10f + r * 10f, e.Position - e.Facing * 10f - r * 10f]; DrawColoredPolygon(h, new Color(0.95f, 0.34f, 0.34f)); DrawPolyline(h, Colors.White, 2f); DrawRect(new Rect2(e.Position + new Vector2(-16f, -24f), new Vector2(32f, 5f)), new Color(0.18f, 0.18f, 0.2f), true); DrawRect(new Rect2(e.Position + new Vector2(-16f, -24f), new Vector2(32f * ((float)e.Hp / 4f), 5f)), new Color(0.95f, 0.35f, 0.35f), true); } }
	private void DrawShots() { foreach (Fx f in _fx) { float a = Mathf.Clamp(f.TimeLeft / 0.12f, 0f, 1f); Color c = f.Color; c.A = a; DrawLine(f.From, f.To, c, 4f); } }
	private void DrawSquad() { for (int i = 0; i < _squad.Count; i++) { Unit u = _squad[i]; if (!u.Alive) continue; Color b = i == 0 ? new Color(0.26f, 0.9f, 1f) : (i == 1 ? new Color(0.34f, 0.78f, 1f) : new Color(0.38f, 0.95f, 0.65f)); Vector2 r = new(-u.Facing.Y, u.Facing.X); Vector2[] h = [u.Position + u.Facing * 18f, u.Position - u.Facing * 10f + r * 11f, u.Position - u.Facing * 10f - r * 11f]; DrawColoredPolygon(h, b); DrawPolyline(h, Colors.White, 2f); DrawRect(new Rect2(u.Position + new Vector2(-18f, -24f), new Vector2(36f, 5f)), new Color(0.18f, 0.18f, 0.2f), true); DrawRect(new Rect2(u.Position + new Vector2(-18f, -24f), new Vector2(36f * ((float)u.Hp / u.MaxHp), 5f)), new Color(0.35f, 0.95f, 0.45f), true); DrawRect(new Rect2(u.Position + new Vector2(-18f, -17f), new Vector2(36f, 4f)), new Color(0.12f, 0.13f, 0.15f), true); DrawRect(new Rect2(u.Position + new Vector2(-18f, -17f), new Vector2(36f * (u.Stamina / u.MaxStamina), 4f)), u.IsRunning ? new Color(1f, 0.72f, 0.26f) : new Color(0.46f, 0.85f, 1f), true); DrawString(ThemeDB.FallbackFont, u.Position + new Vector2(-24f, -30f), u.Name, HorizontalAlignment.Left, -1f, 11, Colors.White); } }
	private void DrawHud()
	{
		DrawRect(new Rect2(new Vector2(18, 18), new Vector2(430, 224)), new Color(0.03f, 0.03f, 0.04f, 0.88f), true);
		DrawRect(new Rect2(new Vector2(18, 18), new Vector2(430, 224)), Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, new Vector2(34, 42), $"状态 {GetStateLabel()}   生还 {_aliveCount}/3   弹药 {TotalAmmo()}", HorizontalAlignment.Left, -1f, 17, Colors.White);
		DrawString(ThemeDB.FallbackFont, new Vector2(34, 68), $"带出价值 {_lootValue}", HorizontalAlignment.Left, -1f, 17, Colors.White);
		DrawString(ThemeDB.FallbackFont, new Vector2(34, 96), _status, HorizontalAlignment.Left, 390f, 14, new Color(0.86f, 0.9f, 0.95f));
		DrawString(ThemeDB.FallbackFont, new Vector2(34, 124), "右键移动小队，按 F 主动撤离。", HorizontalAlignment.Left, -1f, 12, new Color(0.72f, 0.76f, 0.82f));

		for (int i = 0; i < _squad.Count && i < _searchToggleRects.Length; i++)
		{
			Rect2 box = new Rect2(new Vector2(34 + i * 108, 144), new Vector2(96, 28));
			_searchToggleRects[i] = box;
			DrawRect(box, new Color(0.11f, 0.12f, 0.14f), true);
			DrawRect(box, Colors.White, false, 1.5f);
			Rect2 tick = new Rect2(box.Position + new Vector2(8, 6), new Vector2(16, 16));
			DrawRect(tick, new Color(0.06f, 0.06f, 0.08f), true);
			DrawRect(tick, Colors.White, false, 1.2f);
			if (_squad[i].SearchEnabled)
			{
				DrawLine(tick.Position + new Vector2(3, 8), tick.Position + new Vector2(7, 13), new Color(0.55f, 0.95f, 0.6f), 2f);
				DrawLine(tick.Position + new Vector2(7, 13), tick.Position + new Vector2(14, 3), new Color(0.55f, 0.95f, 0.6f), 2f);
			}
			DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(30, 19), $"{_squad[i].Name} 搜索", HorizontalAlignment.Left, -1f, 12, Colors.White);
		}

		for (int i = 0; i < _squad.Count && i < _runToggleRects.Length; i++)
		{
			Rect2 box = new Rect2(new Vector2(34 + i * 108, 176), new Vector2(96, 28));
			_runToggleRects[i] = box;
			DrawRect(box, new Color(0.11f, 0.12f, 0.14f), true);
			DrawRect(box, Colors.White, false, 1.5f);
			Rect2 tick = new Rect2(box.Position + new Vector2(8, 6), new Vector2(16, 16));
			DrawRect(tick, new Color(0.06f, 0.06f, 0.08f), true);
			DrawRect(tick, Colors.White, false, 1.2f);
			if (_squad[i].RunEnabled)
			{
				DrawLine(tick.Position + new Vector2(3, 8), tick.Position + new Vector2(7, 13), new Color(0.55f, 0.95f, 0.6f), 2f);
				DrawLine(tick.Position + new Vector2(7, 13), tick.Position + new Vector2(14, 3), new Color(0.55f, 0.95f, 0.6f), 2f);
			}
			DrawString(ThemeDB.FallbackFont, box.Position + new Vector2(30, 19), $"{_squad[i].Name} Run", HorizontalAlignment.Left, -1f, 12, Colors.White);
		}

		if (_extractProgress > 0f)
		{
			DrawRect(new Rect2(new Vector2(650, 620), new Vector2(180, 18)), new Color(0.09f, 0.09f, 0.11f), true);
			DrawRect(new Rect2(new Vector2(650, 620), new Vector2(180f * Mathf.Clamp(_extractProgress / 1.4f, 0f, 1f), 18)), new Color(0.42f, 0.95f, 0.58f), true);
		}

		DrawContainerPanel(_activeContainer);
	}

	private void DrawContainerPanel(Loot container)
	{
		if (container == null)
		{
			return;
		}

		Vector2 panelPos = new Vector2(760, 18);
		Vector2 cellSize = new Vector2(26, 26);
		Vector2 panelSize = new Vector2(container.GridSize.X * cellSize.X + 38, container.GridSize.Y * cellSize.Y + 72);

		DrawRect(new Rect2(panelPos, panelSize), new Color(0.04f, 0.04f, 0.05f, 0.92f), true);
		DrawRect(new Rect2(panelPos, panelSize), Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panelPos + new Vector2(18, 26), container.Label, HorizontalAlignment.Left, -1f, 16, Colors.White);

		Vector2 gridOrigin = panelPos + new Vector2(18, 42);
		for (int y = 0; y < container.GridSize.Y; y++)
		{
			for (int x = 0; x < container.GridSize.X; x++)
			{
				Rect2 cellRect = new Rect2(gridOrigin + new Vector2(x * cellSize.X, y * cellSize.Y), cellSize - new Vector2(2, 2));
				DrawRect(cellRect, new Color(0.09f, 0.1f, 0.12f), true);
				DrawRect(cellRect, new Color(0.24f, 0.26f, 0.3f), false, 1f);
			}
		}

		foreach (LootItem item in container.Items)
		{
			Rect2 itemRect = new Rect2(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - 2, item.Size.Y * cellSize.Y - 2));

			if (!item.Revealed)
			{
				DrawRect(itemRect, new Color(0.3f, 0.32f, 0.35f), true);
				DrawRect(itemRect, new Color(0.62f, 0.64f, 0.67f), false, 1.2f);
				for (float offset = -itemRect.Size.Y; offset < itemRect.Size.X; offset += 8f)
				{
					Vector2 from = new Vector2(itemRect.Position.X + offset, itemRect.Position.Y + itemRect.Size.Y);
					Vector2 to = new Vector2(itemRect.Position.X + offset + itemRect.Size.Y, itemRect.Position.Y);
					DrawLine(from, to, new Color(0.55f, 0.57f, 0.6f, 0.65f), 1f);
				}
			}
			else
			{
				DrawRect(itemRect, GetRarityColor(item.Rarity), true);
				DrawRect(itemRect, Colors.White, false, 1.4f);
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(4, 16), item.Label, HorizontalAlignment.Left, itemRect.Size.X - 6f, 11, Colors.Black);
			}

			if (item.Searcher != null && !item.Revealed)
			{
				Vector2 center = itemRect.GetCenter();
				float ratio = Mathf.Clamp(item.SearchProgress / item.SearchTime, 0f, 0.98f);
				float endAngle = -Mathf.Pi / 2f + Mathf.Tau * ratio;
				DrawArc(center, 10f, -Mathf.Pi / 2f, endAngle, 24, GetRarityColor(item.Rarity), 3f);
				DrawString(ThemeDB.FallbackFont, center + new Vector2(-12, 24), item.Searcher.Name, HorizontalAlignment.Left, -1f, 10, Colors.White);
			}
		}
	}

	private Vector2 ResolveMove(Vector2 from, float radius, Vector2 desired)
	{
		Vector2 next = ClampInside(desired, radius);
		foreach (Rect2 o in _obstacles)
		{
			Rect2 ex = o.Grow(radius + 1f);
			if (ex.HasPoint(next) || SegmentRect(from, next, ex))
			{
				Vector2 sx = new(next.X, from.Y); Vector2 sy = new(from.X, next.Y);
				bool fx = !ex.HasPoint(sx) && !SegmentRect(from, sx, ex); bool fy = !ex.HasPoint(sy) && !SegmentRect(from, sy, ex);
				next = fx ? sx : (fy ? sy : from);
			}
		}
		return ClampInside(next, radius);
	}

	private Vector2 ClampInside(Vector2 p, float r) => new(Mathf.Clamp(p.X, _map.Position.X + r, _map.End.X - r), Mathf.Clamp(p.Y, _map.Position.Y + r, _map.End.Y - r));
	private bool TryObstacleHit(Vector2 from, Vector2 to, out Vector2 hit, out float dist) { hit = Vector2.Zero; dist = float.MaxValue; bool found = false; foreach (Rect2 o in _obstacles) if (TrySegmentRect(from, to, o, out Vector2 c)) { float d = from.DistanceTo(c); if (d < dist) { dist = d; hit = c; found = true; } } return found; }
	private static bool SegmentRect(Vector2 from, Vector2 to, Rect2 rect) => TrySegmentRect(from, to, rect, out _);
	private static bool TrySegmentRect(Vector2 from, Vector2 to, Rect2 rect, out Vector2 hit)
	{
		hit = Vector2.Zero; if (rect.HasPoint(from)) { hit = from; return true; }
		Vector2[] c = [rect.Position, new Vector2(rect.End.X, rect.Position.Y), rect.End, new Vector2(rect.Position.X, rect.End.Y)];
		float best = float.MaxValue; bool found = false;
		for (int i = 0; i < c.Length; i++) { Variant v = Geometry2D.SegmentIntersectsSegment(from, to, c[i], c[(i + 1) % c.Length]); if (v.VariantType == Variant.Type.Nil) continue; Vector2 p = v.AsVector2(); float d = from.DistanceTo(p); if (d < best) { best = d; hit = p; found = true; } }
		return found;
	}
}
