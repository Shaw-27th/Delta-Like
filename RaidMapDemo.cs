using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RaidMapDemo : Node2D
{
	private const int RecruitCost = 30;
	private const int MapTemplateCount = 2;
	private const int DifficultyCount = 3;
	private const bool EnableCombatFxDebugOpening = true;

	private enum NodeType
	{
		Room,
		Battle,
		Search,
		Extract,
	}

	private enum ContainerKind
	{
		Room,
		CorpsePile,
		EliteCorpse,
	}

	private enum AiIntent
	{
		Idle,
		Moving,
		Clearing,
		Looting,
		Fighting,
		Extracting,
		Extracted,
		Defeated,
	}

	private enum EquipmentSlot
	{
		Weapon,
		Armor,
		Trinket,
	}

	private enum ItemRarity
	{
		White,
		Green,
		Blue,
		Purple,
		Gold,
	}

	private enum OperationDifficulty
	{
		Trial,
		Muster,
		AllIn,
	}

	private sealed class MapNode
	{
		public int Id;
		public string Name = "";
		public NodeType Type;
		public Vector2 Position;
		public readonly List<int> Links = new();
		public int Threat;
		public bool Revealed;
		public bool Visited;
		public bool SearchRewardClaimed;
		public readonly List<LootContainer> Containers = new();
	}

	private sealed class LootContainer
	{
		public string Label = "";
		public ContainerKind Kind;
		public Vector2I GridSize = new(5, 4);
		public readonly List<string> VisibleItems = new();
		public readonly List<string> HiddenItems = new();
		public readonly List<EquippedLoot> EquippedItems = new();
		public readonly List<GridLootItem> GridItems = new();
		public int ActiveSearchItemIndex = -1;
		public int HiddenIndex;
		public bool IsEmpty => CountAvailableGridItems() == 0 && CountUntakenEquippedItems() == 0;
		public int HiddenRemaining => HiddenItems.Count - HiddenIndex;

		private int CountUntakenEquippedItems()
		{
			int count = 0;
			foreach (EquippedLoot equipped in EquippedItems)
			{
				if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
				{
					count++;
				}
			}

			return count;
		}

		private int CountAvailableGridItems()
		{
			if (GridItems.Count == 0)
			{
				return VisibleItems.Count + Mathf.Max(0, HiddenItems.Count - HiddenIndex);
			}

			int count = 0;
			foreach (GridLootItem item in GridItems)
			{
				if (!item.Taken)
				{
					count++;
				}
			}

			return count;
		}
	}

	private sealed class EquippedLoot
	{
		public EquipmentSlot Slot;
		public string Label = "";
		public bool Taken;
	}

	private sealed class GridLootItem
	{
		public string Label = "";
		public ItemRarity Rarity;
		public Vector2I Size;
		public Vector2I Cell;
		public bool Revealed;
		public bool Taken;
		public float SearchTime;
		public float SearchProgress;
	}

	private sealed class ShopEntry
	{
		public string Label = "";
		public int Price;
	}

	private sealed class SoldierRecord
	{
		public string Name = "";
	}

	private sealed class AiSquad
	{
		public string Name = "";
		public int NodeId;
		public int Strength;
		public int Supplies;
		public AiIntent Intent;
		public int IntentTargetNodeId = -1;
		public int BusyTurns;
		public int RivalId = -1;
		public readonly List<string> Loot = new();
		public bool IsAlive => Intent != AiIntent.Defeated && Intent != AiIntent.Extracted && Strength > 0;
	}

	private sealed class Encounter
	{
		public string EnemyName = "";
		public int TurnCost;
		public int EnemyPower;
		public bool EnemyHasElite;
		public MapNode Node;
		public AiSquad Squad;
		public string PromptText = "";
	}

	private enum RoomDoorSide
	{
		Left,
		Top,
		Right,
		Bottom,
	}

	private sealed class RoomUnit
	{
		public bool IsPlayerSide;
		public bool IsHero;
		public bool IsElite;
		public bool IsRanged;
		public string Name = "";
		public Vector2 Position;
		public Vector2 Facing = Vector2.Right;
		public float Speed;
		public int Hp;
		public int MaxHp;
		public int DamageMin;
		public int DamageMax;
		public float AttackRange;
		public float AttackCooldown;
		public float AttackCycleScale = 1f;
		public float AttackWindupTime;
		public float RecoveryTime;
		public float StaggerTime;
		public float HitPauseTime;
		public RoomUnit PendingAttackTarget;
		public int PendingAttackDamage;
		public float PendingAttackRangeSlack;
		public float PendingAttackLungeDistance;
		public bool PendingAttackHeavy;
		public float HitFlash;
		public Vector2 KnockbackVelocity;
		public float KnockbackTime;
		public bool IsAlive => Hp > 0;
	}

private sealed class RoomProjectileEffect
	{
		public Vector2 Position;
		public Vector2 PreviousPosition;
		public Vector2 Velocity;
		public float TimeLeft;
		public float Duration;
		public bool PlayerSide;
		public bool Heavy;
		public RoomUnit Target;
		public int Damage;
	}

	private sealed class RoomMeleeArcEffect
	{
		public Vector2 Origin;
		public float FacingAngle;
		public float Range;
		public float ArcHalfAngle;
		public float TimeLeft;
		public float Duration;
		public bool PlayerSide;
		public bool Heavy;
		public RoomUnit Owner;
	}

	private readonly struct ButtonDef
	{
		public ButtonDef(Rect2 rect, string action, int index = -1)
		{
			Rect = rect;
			Action = action;
			Index = index;
		}

		public Rect2 Rect { get; }
		public string Action { get; }
		public int Index { get; }
	}

	private readonly List<MapNode> _nodes = new();
	private readonly List<AiSquad> _aiSquads = new();
	private readonly List<string> _eventLog = new();
	private readonly List<ButtonDef> _buttons = new();
	private readonly List<string> _runBackpack = new();
	private readonly List<string> _stash = new();
	private readonly List<ShopEntry> _shopStock = new();
	private readonly List<SoldierRecord> _soldierRoster = new();
	private readonly List<SoldierRecord> _runSoldiers = new();
	private readonly List<RoomUnit> _roomUnits = new();
	private readonly List<RoomProjectileEffect> _roomProjectileEffects = new();
	private readonly List<RoomMeleeArcEffect> _roomMeleeArcEffects = new();
	private readonly RandomNumberGenerator _rng = new();

	private Rect2 _mapRect = new(new Vector2(30f, 30f), new Vector2(760f, 660f));
	private Rect2 _sideRect = new(new Vector2(810f, 30f), new Vector2(360f, 660f));

	private Encounter _encounter;
	private Encounter _pendingEncounter;
	private RoomBattleSim _battleSim;
	private int _playerNodeId;
	private int _turn;
	private int _playerHp;
	private int _playerMaxHp;
	private int _playerStrength;
	private int _timeSlotProgress;
	private int _searchActions;
	private int _lootValue;
	private int _money;
	private int _nextSoldierId = 1;
	private bool _runEnded;
	private bool _runFailed;
	private bool _inHideout = true;
	private bool _autoSearchEnabled;
	private bool _skipSearchConfirm;
	private bool _showSearchConfirm;
	private bool _confirmSkipChecked;
	private int _selectedContainerIndex = -1;
	private int _pendingRevealContainerIndex = -1;
	private bool _showMapOverlay;
	private int _plannedExitNodeId = -1;
	private bool _isPlayerMoving;
	private int _moveTargetNodeId = -1;
	private float _moveProgress;
	private Vector2 _playerMarkerPosition;
	private int _selectedMapTemplate;
	private OperationDifficulty _selectedDifficulty = OperationDifficulty.Trial;
	private Vector2 _heroMoveTarget;
	private bool _heroHasMoveTarget;
	private int _pendingExitNodeId = -1;
	private RoomDoorSide _pendingExitSide = RoomDoorSide.Right;
	private bool _roomDirty;
	private string _status = "点击相邻节点移动。";

	public override void _Ready()
	{
		_rng.Randomize();
		UpdateLayoutRects();
		InitHideout();
	}

	public override void _Process(double delta)
	{
		UpdateLayoutRects();
		if (!_inHideout)
		{
			UpdateContainerSearch((float)delta);
			UpdatePlayerMove((float)delta);
			UpdateRoomSimulation((float)delta);
		}
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		UpdateLayoutRects();
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo && keyEvent.Keycode == Key.M)
		{
			ToggleMapOverlay();
			return;
		}

		if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		Vector2 click = mouse.Position;
		for (int i = _buttons.Count - 1; i >= 0; i--)
		{
			if (_buttons[i].Rect.HasPoint(click))
			{
				HandleButton(_buttons[i]);
				return;
			}
		}

		if (_inHideout || _runEnded || _selectedContainerIndex >= 0)
		{
			return;
		}

		if (_showMapOverlay)
		{
			foreach (MapNode node in _nodes)
			{
				if (node.Position.DistanceTo(click) <= 24f)
				{
					TryPlanExitToNode(node.Id);
					return;
				}
			}
			return;
		}

		Rect2 roomRect = GetRoomArenaRect();
		if (roomRect.HasPoint(click))
		{
			_heroMoveTarget = ClampToRoom(click);
			_heroHasMoveTarget = true;
			if (_pendingExitNodeId < 0)
			{
				_status = "主英雄正在移动，小兵将跟随。";
			}
		}
	}

	public override void _Draw()
	{
		UpdateLayoutRects();
		_buttons.Clear();
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0.06f, 0.07f, 0.09f), true);
		if (_inHideout)
		{
			DrawHideout();
			return;
		}
		DrawRoomViewUnified();
		if (_showMapOverlay)
		{
			DrawMapOverlay();
		}
		DrawSidePanel();
		if (_selectedContainerIndex >= 0)
		{
			DrawContainerPopup();
		}
		if (_runEnded)
		{
			DrawEndOverlay();
		}
	}

	private void UpdateLayoutRects()
	{
		Vector2 viewport = GetViewportRect().Size;
		float outerMargin = viewport.X >= 1500f ? 36f : 24f;
		float topMargin = viewport.Y >= 860f ? 32f : 24f;
		float bottomMargin = topMargin;
		float gap = viewport.X >= 1500f ? 28f : 20f;
		float sideWidth = Mathf.Clamp(viewport.X * 0.29f, 360f, 460f);
		float mapWidth = Mathf.Max(640f, viewport.X - outerMargin * 2f - gap - sideWidth);
		float panelHeight = Mathf.Max(620f, viewport.Y - topMargin - bottomMargin);

		_mapRect = new Rect2(new Vector2(outerMargin, topMargin), new Vector2(mapWidth, panelHeight));
		_sideRect = new Rect2(new Vector2(_mapRect.End.X + gap, topMargin), new Vector2(sideWidth, panelHeight));
	}

	private void ResetRun()
	{
		_inHideout = false;
		_nodes.Clear();
		_aiSquads.Clear();
		_eventLog.Clear();
		_runBackpack.Clear();

		_playerMaxHp = 24;
		_playerHp = 24;
		_playerStrength = 11;
		_timeSlotProgress = 0;
		_lootValue = 0;
		_turn = 0;
		_runEnded = false;
		_runFailed = false;
		_autoSearchEnabled = false;
		_showMapOverlay = false;
		_plannedExitNodeId = -1;
		_selectedContainerIndex = -1;
		_encounter = null;
		_roomUnits.Clear();
		_roomProjectileEffects.Clear();
		_roomMeleeArcEffects.Clear();
		_heroHasMoveTarget = false;
		_pendingExitNodeId = -1;
		_roomDirty = true;
		_runSoldiers.Clear();
		foreach (SoldierRecord soldier in _soldierRoster)
		{
			_runSoldiers.Add(new SoldierRecord { Name = soldier.Name });
		}

		if (EnableCombatFxDebugOpening)
		{
			_playerMaxHp = 260;
			_playerHp = 260;
			_runSoldiers.Clear();
			_runSoldiers.Add(new SoldierRecord { Name = "前锋测试机" });
		}

		_playerStrength = 3 + _runSoldiers.Count;

		if (_selectedMapTemplate == 1)
		{
			BuildBorderKeepMap();
			ApplyCombatFxDebugOpening();
			ApplyDifficultyToRun();
			LogEvent("行动开始，其他小队已经进入边境堡寨。");
			EnterNodeRoom(_playerNodeId, RoomDoorSide.Left, false);
			RefreshStatus();
			return;
		}

		AddNode(0, "入口前庭", NodeType.Room, new Vector2(105f, 360f), 0);
		AddNode(1, "墓园", NodeType.Search, new Vector2(135f, 235f), 1);
		AddNode(2, "马厩", NodeType.Room, new Vector2(140f, 500f), 1);
		AddNode(3, "抄经室", NodeType.Search, new Vector2(210f, 180f), 0);
		AddNode(4, "回廊北段", NodeType.Battle, new Vector2(340f, 180f), 4);
		AddNode(5, "中庭", NodeType.Room, new Vector2(375f, 305f), 2);
		AddNode(6, "宿舍", NodeType.Battle, new Vector2(240f, 515f), 4);
		AddNode(7, "食堂", NodeType.Search, new Vector2(385f, 515f), 1);
		AddNode(8, "院长书房", NodeType.Search, new Vector2(520f, 120f), 2);
		AddNode(9, "礼拜堂", NodeType.Battle, new Vector2(610f, 190f), 5);
		AddNode(10, "圣物库", NodeType.Search, new Vector2(685f, 275f), 3);
		AddNode(11, "钟楼", NodeType.Room, new Vector2(595f, 380f), 2);
		AddNode(12, "地窖", NodeType.Battle, new Vector2(595f, 515f), 6);
		AddNode(13, "北侧撤离点", NodeType.Extract, new Vector2(735f, 110f), 0);
		AddNode(14, "地下水道出口", NodeType.Extract, new Vector2(735f, 560f), 0);

		LinkNodes(0, 1); LinkNodes(0, 2); LinkNodes(0, 5);
		LinkNodes(1, 3); LinkNodes(1, 4);
		LinkNodes(2, 6); LinkNodes(2, 5);
		LinkNodes(3, 4); LinkNodes(3, 8);
		LinkNodes(4, 5); LinkNodes(4, 8); LinkNodes(4, 9);
		LinkNodes(5, 6); LinkNodes(5, 7); LinkNodes(5, 10); LinkNodes(5, 11);
		LinkNodes(6, 7);
		LinkNodes(7, 12);
		LinkNodes(8, 9); LinkNodes(8, 13);
		LinkNodes(9, 10); LinkNodes(9, 13);
		LinkNodes(10, 11);
		LinkNodes(11, 12); LinkNodes(11, 14);
		LinkNodes(12, 14);

		_nodes[0].Visited = true;
		_playerNodeId = 0;
		_playerMarkerPosition = _nodes[0].Position;
		_isPlayerMoving = false;
		_moveTargetNodeId = -1;
		_moveProgress = 0f;
		UpdateVision(_playerNodeId);

		AddRoomContainer(1, "墓地供箱", 3, 0);
		AddRoomContainer(2, "马料柜", 2, 0);
		AddRoomContainer(3, "文档箱", 4, 1);
		AddRoomContainer(5, "遗弃背包", 2, 0);
		AddRoomContainer(7, "食材柜", 3, 0);
		AddRoomContainer(8, "书房暗柜", 4, 1);
		AddRoomContainer(10, "圣物架", 5, 2);
		AddRoomContainer(10, "祭器匣", 4, 1);
		AddRoomContainer(11, "钟楼杂物箱", 3, 0);
		AddRoomContainer(12, "地窖锁柜", 5, 1);

		_aiSquads.Add(new AiSquad { Name = "赤狼小队", NodeId = 10, Strength = 9, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "蓝鸦小队", NodeId = 6, Strength = 8, Supplies = 2, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "金狮小队", NodeId = 3, Strength = 7, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "灰烬小队", NodeId = 12, Strength = 10, Supplies = 2, Intent = AiIntent.Idle });

		ApplyCombatFxDebugOpening();
		ApplyDifficultyToRun();
		LogEvent("行动开始，其他小队已经进入地图。");
		EnterNodeRoom(_playerNodeId, RoomDoorSide.Left, false);
		RefreshStatus();
	}

	private void ApplyCombatFxDebugOpening()
	{
		if (!EnableCombatFxDebugOpening || _nodes.Count == 0)
		{
			return;
		}

		_nodes[0].Name = "近战特效调试场";
		_nodes[0].Threat = 0;
	}

	private void InitHideout()
	{
		_inHideout = true;
		_runEnded = false;
		_runFailed = false;
		_selectedContainerIndex = -1;
		_battleSim = null;
		_eventLog.Clear();
		if (_shopStock.Count == 0)
		{
			SeedShop();
		}
		if (_money == 0 && _stash.Count == 0)
		{
			_money = 300;
			_stash.Add("旧军刀");
			_stash.Add("草药包");
		}
		if (_soldierRoster.Count == 0 && _nextSoldierId == 1)
		{
			RecruitSoldierInternal();
			RecruitSoldierInternal();
		}
	}

	private void SeedShop()
	{
		_shopStock.Clear();
		_shopStock.Add(new ShopEntry { Label = "军用口粮", Price = 18 });
		_shopStock.Add(new ShopEntry { Label = "草药包", Price = 22 });
		_shopStock.Add(new ShopEntry { Label = "钢制短刀", Price = 40 });
		_shopStock.Add(new ShopEntry { Label = "银质宝石", Price = 65 });
	}

	private void AddNode(int id, string name, NodeType type, Vector2 position, int threat)
	{
		_nodes.Add(new MapNode { Id = id, Name = name, Type = type, Position = position, Threat = threat });
	}

	private void LinkNodes(int a, int b)
	{
		_nodes[a].Links.Add(b);
		_nodes[b].Links.Add(a);
	}

	private void AddRoomContainer(int nodeId, string label, int hiddenCount, int visibleCount)
	{
		LootContainer container = new() { Label = label, Kind = ContainerKind.Room };
		for (int i = 0; i < visibleCount; i++) container.VisibleItems.Add(RollVisibleEquipment());
		for (int i = 0; i < hiddenCount; i++) container.HiddenItems.Add(RollLootItem());
		_nodes[nodeId].Containers.Add(container);
	}

	private void ApplyDifficultyToRun()
	{
		float threatScale = GetDifficultyThreatScale(_selectedDifficulty);
		float lootScale = GetDifficultyLootScale(_selectedDifficulty);
		foreach (MapNode node in _nodes)
		{
			if (node.Threat > 0)
			{
				node.Threat = Mathf.Max(1, Mathf.RoundToInt(node.Threat * threatScale));
			}

			foreach (LootContainer container in node.Containers)
			{
				if (lootScale >= 2f)
				{
					container.HiddenItems.Add(RollLootItem());
					if (node.Type == NodeType.Search)
					{
						container.HiddenItems.Add(RollLootItem());
					}
				}

				if (lootScale >= 4f)
				{
					container.VisibleItems.Add(RollVisibleEquipment());
					container.HiddenItems.Add(RollLootItem());
				}
			}
		}

		foreach (AiSquad squad in _aiSquads)
		{
			squad.Strength = Mathf.Max(1, Mathf.RoundToInt(squad.Strength * threatScale));
			if (lootScale >= 2f)
			{
				squad.Supplies += 1;
			}
			if (lootScale >= 4f)
			{
				squad.Supplies += 1;
			}
		}
	}

	private void BuildBorderKeepMap()
	{
		AddNode(0, "南侧营门", NodeType.Room, new Vector2(110f, 565f), 0);
		AddNode(1, "外壕", NodeType.Battle, new Vector2(200f, 505f), 3);
		AddNode(2, "军需棚", NodeType.Search, new Vector2(185f, 370f), 1);
		AddNode(3, "西侧城墙", NodeType.Battle, new Vector2(230f, 220f), 4);
		AddNode(4, "操练场", NodeType.Room, new Vector2(365f, 470f), 2);
		AddNode(5, "主庭", NodeType.Room, new Vector2(390f, 315f), 2);
		AddNode(6, "兵营", NodeType.Search, new Vector2(355f, 165f), 1);
		AddNode(7, "军械库", NodeType.Search, new Vector2(525f, 180f), 3);
		AddNode(8, "指挥厅", NodeType.Battle, new Vector2(560f, 315f), 5);
		AddNode(9, "东侧塔楼", NodeType.Battle, new Vector2(670f, 215f), 4);
		AddNode(10, "辎重院", NodeType.Search, new Vector2(640f, 460f), 2);
		AddNode(11, "地牢", NodeType.Battle, new Vector2(500f, 565f), 5);
		AddNode(12, "北门撤离点", NodeType.Extract, new Vector2(720f, 95f), 0);
		AddNode(13, "河道撤离点", NodeType.Extract, new Vector2(735f, 565f), 0);

		LinkNodes(0, 1); LinkNodes(0, 2);
		LinkNodes(1, 2); LinkNodes(1, 4);
		LinkNodes(2, 3); LinkNodes(2, 5);
		LinkNodes(3, 6);
		LinkNodes(4, 5); LinkNodes(4, 10); LinkNodes(4, 11);
		LinkNodes(5, 6); LinkNodes(5, 7); LinkNodes(5, 8);
		LinkNodes(6, 7);
		LinkNodes(7, 8); LinkNodes(7, 9);
		LinkNodes(8, 9); LinkNodes(8, 10); LinkNodes(8, 11);
		LinkNodes(9, 12);
		LinkNodes(10, 11); LinkNodes(10, 13);
		LinkNodes(11, 13);

		_nodes[0].Visited = true;
		_playerNodeId = 0;
		_playerMarkerPosition = _nodes[0].Position;
		_isPlayerMoving = false;
		_moveTargetNodeId = -1;
		_moveProgress = 0f;
		UpdateVision(_playerNodeId);

		AddRoomContainer(2, "军需木箱", 4, 1);
		AddRoomContainer(4, "操练场杂物架", 2, 0);
		AddRoomContainer(6, "兵营储物柜", 4, 1);
		AddRoomContainer(7, "军械架", 5, 2);
		AddRoomContainer(8, "指挥文书柜", 4, 1);
		AddRoomContainer(10, "辎重车", 5, 1);
		AddRoomContainer(11, "地牢锁箱", 5, 1);

		_aiSquads.Add(new AiSquad { Name = "赤狼小队", NodeId = 9, Strength = 9, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "蓝鸦小队", NodeId = 6, Strength = 8, Supplies = 2, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "金狮小队", NodeId = 2, Strength = 7, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "灰烬小队", NodeId = 11, Strength = 10, Supplies = 2, Intent = AiIntent.Idle });
	}

	private void UpdateVision(int centerNodeId)
	{
		if (centerNodeId < 0 || centerNodeId >= _nodes.Count)
		{
			return;
		}

		MapNode center = _nodes[centerNodeId];
		center.Revealed = true;
		foreach (int link in center.Links)
		{
			_nodes[link].Revealed = true;
		}
	}

	private bool HasClearVision(MapNode node)
	{
		if (node.Id == _playerNodeId)
		{
			return true;
		}

		return _nodes[_playerNodeId].Links.Contains(node.Id);
	}

	private void UpdatePlayerMove(float delta)
	{
		if (!_isPlayerMoving || _moveTargetNodeId < 0 || _moveTargetNodeId >= _nodes.Count)
		{
			return;
		}

		_moveProgress = Mathf.Min(1f, _moveProgress + delta * 2.6f);
		Vector2 from = _nodes[_playerNodeId].Position;
		Vector2 to = _nodes[_moveTargetNodeId].Position;
		_playerMarkerPosition = from.Lerp(to, _moveProgress);
		if (_moveProgress >= 1f)
		{
			CompletePlayerMove();
		}
	}

	private void CompletePlayerMove()
	{
		if (_moveTargetNodeId < 0 || _moveTargetNodeId >= _nodes.Count)
		{
			_isPlayerMoving = false;
			return;
		}

		_playerNodeId = _moveTargetNodeId;
		_nodes[_playerNodeId].Visited = true;
		UpdateVision(_playerNodeId);
		_playerMarkerPosition = _nodes[_playerNodeId].Position;
		_moveTargetNodeId = -1;
		_moveProgress = 0f;
		_isPlayerMoving = false;
		HandleArrival(_nodes[_playerNodeId]);
	}

	private Rect2 GetRoomArenaRect()
	{
		return new Rect2(_mapRect.Position + new Vector2(40f, 60f), _mapRect.Size - new Vector2(80f, 100f));
	}

	private Vector2 ClampToRoom(Vector2 position)
	{
		Rect2 rect = GetRoomArenaRect();
		return new Vector2(
			Mathf.Clamp(position.X, rect.Position.X + 18f, rect.End.X - 18f),
			Mathf.Clamp(position.Y, rect.Position.Y + 18f, rect.End.Y - 18f));
	}

	private void EnterNodeRoom(int nodeId, RoomDoorSide entrySide, bool advanceTurn)
	{
		int previousNodeId = _playerNodeId;
		_playerNodeId = nodeId;
		_nodes[_playerNodeId].Visited = true;
		UpdateVision(_playerNodeId);
		_pendingExitNodeId = -1;
		_heroHasMoveTarget = false;

		Vector2 heroSpawn = GetDoorSpawnPoint(entrySide);
		if (_roomUnits.Count == 0)
		{
			SpawnAlliesAt(heroSpawn);
		}
		else
		{
			RoomUnit hero = FindHeroUnit();
			if (hero != null)
			{
				hero.Position = heroSpawn;
				hero.Facing = Vector2.Right;
				hero.KnockbackTime = 0f;
				hero.KnockbackVelocity = Vector2.Zero;
			}
			RelayoutAlliesAroundHero();
		}

		RebuildRoomEnemies();
		_playerMarkerPosition = _nodes[_playerNodeId].Position;
		_roomDirty = false;

		if (advanceTurn && previousNodeId != nodeId)
		{
			AdvanceTurn($"穿过房门进入 {_nodes[nodeId].Name}。");
		}
		RefreshStatus();
	}

	private Vector2 GetDoorSpawnPoint(RoomDoorSide side)
	{
		Rect2 rect = GetRoomArenaRect();
		Vector2 p = side switch
		{
			RoomDoorSide.Left => new Vector2(rect.Position.X + 34f, rect.GetCenter().Y),
			RoomDoorSide.Top => new Vector2(rect.GetCenter().X, rect.Position.Y + 34f),
			RoomDoorSide.Right => new Vector2(rect.End.X - 34f, rect.GetCenter().Y),
			_ => new Vector2(rect.GetCenter().X, rect.End.Y - 34f),
		};
		return ClampToRoom(p);
	}

	private void SpawnAlliesAt(Vector2 heroPos)
	{
		_roomUnits.Clear();
		if (EnableCombatFxDebugOpening && _playerNodeId == 0)
		{
			SpawnDebugAlliesAt(heroPos);
			return;
		}

		RoomUnit hero = CreateRoomUnit(true, true, false, true, "英雄", heroPos);
		hero.Hp = _playerHp;
		hero.MaxHp = _playerMaxHp;
		hero.DamageMin = 2;
		hero.DamageMax = 5;
		hero.AttackRange = 164f;
		hero.Speed = 165f;
		_roomUnits.Add(hero);

		for (int i = 0; i < _runSoldiers.Count; i++)
		{
			Vector2 offset = new Vector2(-28f - (i % 3) * 20f, (i / 3) * 24f - 24f);
			RoomUnit soldier = CreateRoomUnit(true, false, false, false, _runSoldiers[i].Name, ClampToRoom(heroPos + offset));
			soldier.Hp = 8;
			soldier.MaxHp = 8;
			soldier.DamageMin = 1;
			soldier.DamageMax = 3;
			soldier.AttackRange = 28f;
			soldier.Speed = 152f;
			_roomUnits.Add(soldier);
		}
	}

	private void SpawnDebugAlliesAt(Vector2 heroPos)
	{
		RoomUnit ranger = CreateRoomUnit(true, true, false, true, "蓝羽试射手", ClampToRoom(heroPos + new Vector2(-12f, -26f)));
		ranger.Hp = 260;
		ranger.MaxHp = 260;
		ranger.DamageMin = 2;
		ranger.DamageMax = 4;
		ranger.AttackRange = 196f;
		ranger.Speed = 118f;
		ranger.AttackCycleScale = 2f;
		_roomUnits.Add(ranger);

		RoomUnit vanguard = CreateRoomUnit(true, false, true, false, "前锋测试机", ClampToRoom(heroPos + new Vector2(-28f, 26f)));
		vanguard.Hp = 300;
		vanguard.MaxHp = 300;
		vanguard.DamageMin = 2;
		vanguard.DamageMax = 4;
		vanguard.AttackRange = 28f;
		vanguard.Speed = 120f;
		vanguard.AttackCycleScale = 2f;
		_roomUnits.Add(vanguard);
	}

	private RoomUnit CreateRoomUnit(bool isPlayerSide, bool isHero, bool isElite, bool isRanged, string name, Vector2 position)
	{
		return new RoomUnit
		{
			IsPlayerSide = isPlayerSide,
			IsHero = isHero,
			IsElite = isElite,
			IsRanged = isRanged,
			Name = name,
			Position = position,
			Facing = isPlayerSide ? Vector2.Right : Vector2.Left,
			Speed = isHero ? 162f : 150f,
			Hp = isHero ? _playerHp : 8,
			MaxHp = isHero ? _playerMaxHp : 8,
			DamageMin = isHero ? 2 : 1,
			DamageMax = isHero ? 5 : 3,
			AttackRange = isRanged ? 160f : 28f,
			AttackCooldown = 0f,
			AttackCycleScale = 1f,
		};
	}

	private void RebuildRoomEnemies()
	{
		for (int i = _roomUnits.Count - 1; i >= 0; i--)
		{
			if (!_roomUnits[i].IsPlayerSide)
			{
				_roomUnits.RemoveAt(i);
			}
		}

		MapNode node = _nodes[_playerNodeId];
		if (EnableCombatFxDebugOpening && node.Id == 0)
		{
			SpawnDebugEnemies();
			return;
		}

		AiSquad squad = GetSquadAtNode(node.Id);
		if (squad != null)
		{
			SpawnSquadEnemies(squad);
		}
		else if (node.Threat > 0)
		{
			SpawnThreatEnemies(node.Threat);
		}
	}

	private void SpawnDebugEnemies()
	{
		Rect2 rect = GetRoomArenaRect();
		RoomUnit ranger = CreateRoomUnit(false, false, true, true, "赤眼试射手", ClampToRoom(new Vector2(rect.End.X - 120f, rect.GetCenter().Y - 26f)));
		ranger.Hp = 260;
		ranger.MaxHp = 260;
		ranger.DamageMin = 2;
		ranger.DamageMax = 4;
		ranger.AttackRange = 196f;
		ranger.Speed = 118f;
		ranger.AttackCycleScale = 2f;
		_roomUnits.Add(ranger);

		RoomUnit raider = CreateRoomUnit(false, false, true, false, "斩刃试作体", ClampToRoom(new Vector2(rect.End.X - 148f, rect.GetCenter().Y + 28f)));
		raider.Hp = 300;
		raider.MaxHp = 300;
		raider.DamageMin = 2;
		raider.DamageMax = 4;
		raider.AttackRange = 28f;
		raider.Speed = 120f;
		raider.AttackCycleScale = 2f;
		_roomUnits.Add(raider);
	}

	private void SpawnThreatEnemies(int threat)
	{
		Rect2 rect = GetRoomArenaRect();
		int count = Mathf.Clamp(1 + threat / 2, 2, 6);
		for (int i = 0; i < count; i++)
		{
			Vector2 p = ClampToRoom(new Vector2(rect.End.X - 74f - (i % 3) * 20f, rect.GetCenter().Y + (i / 3) * 28f - 28f));
			RoomUnit enemy = CreateRoomUnit(false, false, false, i % 3 == 0, "守军", p);
			enemy.Hp = 6 + threat;
			enemy.MaxHp = enemy.Hp;
			enemy.DamageMin = 1 + threat / 3;
			enemy.DamageMax = 3 + threat / 2;
			enemy.AttackRange = enemy.IsRanged ? 156f : 28f;
			_roomUnits.Add(enemy);
		}
	}

	private void SpawnSquadEnemies(AiSquad squad)
	{
		Rect2 rect = GetRoomArenaRect();
		RoomUnit elite = CreateRoomUnit(false, false, true, true, $"{squad.Name} 队长", ClampToRoom(new Vector2(rect.End.X - 76f, rect.GetCenter().Y - 26f)));
		elite.Hp = 12 + squad.Strength / 2;
		elite.MaxHp = elite.Hp;
		elite.DamageMin = 3;
		elite.DamageMax = 6;
		elite.AttackRange = 172f;
		_roomUnits.Add(elite);

		int count = Mathf.Clamp(squad.Strength / 2, 3, 7);
		for (int i = 0; i < count; i++)
		{
			Vector2 p = ClampToRoom(new Vector2(rect.End.X - 98f - (i % 4) * 20f, rect.GetCenter().Y + (i / 4) * 28f + 8f));
			RoomUnit enemy = CreateRoomUnit(false, false, false, i % 3 == 1, "敌兵", p);
			enemy.Hp = 7;
			enemy.MaxHp = 7;
			enemy.DamageMin = 1;
			enemy.DamageMax = 3;
			enemy.AttackRange = enemy.IsRanged ? 152f : 28f;
			_roomUnits.Add(enemy);
		}
	}

	private RoomUnit FindHeroUnit()
	{
		foreach (RoomUnit unit in _roomUnits)
		{
			if (unit.IsPlayerSide && unit.IsHero && unit.IsAlive)
			{
				return unit;
			}
		}
		return null;
	}

	private bool HasHostilesInRoom()
	{
		foreach (RoomUnit unit in _roomUnits)
		{
			if (!unit.IsPlayerSide && unit.IsAlive)
			{
				return true;
			}
		}
		return false;
	}

	private RoomUnit FindNearestTarget(RoomUnit source, bool enemySide)
	{
		RoomUnit best = null;
		float bestDist = float.MaxValue;
		foreach (RoomUnit unit in _roomUnits)
		{
			if (!unit.IsAlive || unit.IsPlayerSide != enemySide)
			{
				continue;
			}

			float dist = source.Position.DistanceTo(unit.Position);
			if (dist < bestDist)
			{
				bestDist = dist;
				best = unit;
			}
		}
		return best;
	}

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
			return;
		}

		if (hero.HitPauseTime > 0f)
		{
			hero.Facing = hero.Facing == Vector2.Zero ? Vector2.Right : hero.Facing.Normalized();
		}
		else if (hero.KnockbackTime > 0f)
		{
			AdvanceKnockback(hero, delta);
		}
		else if (hero.StaggerTime > 0f || hero.AttackWindupTime > 0f || hero.RecoveryTime > 0f)
		{
			hero.Facing = hero.Facing == Vector2.Zero ? Vector2.Right : hero.Facing.Normalized();
		}
		else if (_heroHasMoveTarget)
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
				hero.Position = ClampToRoom(hero.Position + dir * hero.Speed * delta);
			}
		}

		bool hasHostiles = HasHostilesInRoom();
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
				Vector2 follow = hero.Position + new Vector2(-24f, 0f);
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

		if (HasHostilesInRoom())
		{
			RoomUnit heroTarget = FindNearestTarget(hero, false);
			if (heroTarget != null && !_heroHasMoveTarget)
			{
				StepUnitCombat(hero, heroTarget, delta);
			}
		}

		if (_pendingExitNodeId >= 0)
		{
			Rect2 door = GetRoomExitRect(_pendingExitSide).Grow(8f);
			if (door.HasPoint(hero.Position))
			{
				RoomDoorSide entrySide = GetOppositeSide(_pendingExitSide);
				int nextNodeId = _pendingExitNodeId;
				_pendingExitNodeId = -1;
				EnterNodeRoom(nextNodeId, entrySide, true);
			}
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
			else
			{
				_playerHp = 0;
				_runBackpack.Clear();
				_runEnded = true;
				_runFailed = true;
			}
		}
	}

	private void StepUnitCombat(RoomUnit attacker, RoomUnit target, float delta)
	{
		Vector2 toTarget = target.Position - attacker.Position;
		float distance = toTarget.Length();
		if (distance <= 0.001f)
		{
			return;
		}

		Vector2 dir = toTarget / distance;
		attacker.Facing = dir;
		if (!attacker.IsRanged && !target.IsRanged && distance <= attacker.AttackRange + 12f)
		{
			Vector2 side = new(-dir.Y, dir.X);
			float phase = _turn + attacker.Position.X * 0.013f + attacker.Position.Y * 0.007f;
			float weave = Mathf.Sin(Time.GetTicksMsec() * 0.006f + phase);
			float cooldownBase = Mathf.Max(0.01f, 0.46f * attacker.AttackCycleScale);
			float cooldownRatio = Mathf.Clamp(attacker.AttackCooldown / cooldownBase, 0f, 1f);
			bool spacingOut = attacker.AttackCooldown > 0f || attacker.RecoveryTime > 0f;
			float desiredGap = spacingOut
				? Mathf.Max(24f, attacker.AttackRange + 20f + cooldownRatio * 14f)
				: Mathf.Max(18f, attacker.AttackRange + 2f);
			Vector2 desired = target.Position - dir * desiredGap + side * weave * 10f;
			Vector2 pushAway = (attacker.Position - target.Position).Normalized();
			if (pushAway == Vector2.Zero)
			{
				pushAway = attacker.IsPlayerSide ? Vector2.Left : Vector2.Right;
			}

			float tooCloseRatio = desiredGap > 0f ? Mathf.Clamp((desiredGap - distance) / desiredGap, 0f, 1f) : 0f;
			Vector2 contactAdjust = spacingOut
				? side * weave * (16f + cooldownRatio * 10f) + pushAway * (22f + cooldownRatio * 18f + tooCloseRatio * 20f)
				: side * weave * 9f + pushAway * (6f + tooCloseRatio * 6f);
			Vector2 move = (desired + contactAdjust) - attacker.Position;
			if (move.LengthSquared() > 1f)
			{
				float duelMoveScale = spacingOut ? 0.96f : 0.62f;
				attacker.Position = ClampToRoom(attacker.Position + move.Normalized() * attacker.Speed * duelMoveScale * delta);
			}
		}

		if (distance > attacker.AttackRange)
		{
			attacker.Position = ClampToRoom(attacker.Position + dir * attacker.Speed * delta);
			return;
		}

		if (attacker.AttackCooldown > 0f || attacker.AttackWindupTime > 0f || attacker.RecoveryTime > 0f || attacker.StaggerTime > 0f || attacker.HitPauseTime > 0f)
		{
			return;
		}

		attacker.PendingAttackTarget = target;
		attacker.PendingAttackDamage = _rng.RandiRange(attacker.DamageMin, attacker.DamageMax);
		attacker.PendingAttackHeavy = attacker.IsHero || attacker.IsElite;
		attacker.PendingAttackRangeSlack = attacker.IsRanged ? 28f : 14f;
		attacker.PendingAttackLungeDistance = 0f;
		attacker.AttackWindupTime = attacker.IsRanged ? 0.13f : (attacker.PendingAttackHeavy ? 0.12f : 0.09f);
		attacker.RecoveryTime = attacker.IsRanged ? 0.1f : 0.08f;
		float baseCooldown = attacker.IsRanged ? 0.42f : (attacker.PendingAttackHeavy ? 0.54f : 0.46f);
		attacker.AttackCooldown = baseCooldown * Mathf.Max(0.1f, attacker.AttackCycleScale);
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
		unit.HitFlash = Mathf.Max(0f, unit.HitFlash - delta);
		unit.StaggerTime = Mathf.Max(0f, unit.StaggerTime - delta);
		unit.RecoveryTime = Mathf.Max(0f, unit.RecoveryTime - delta);
		unit.KnockbackTime = Mathf.Max(0f, unit.KnockbackTime - delta);

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
		attacker.PendingAttackTarget = null;
		attacker.PendingAttackDamage = 0;
		attacker.PendingAttackRangeSlack = 0f;
		attacker.PendingAttackLungeDistance = 0f;
		attacker.PendingAttackHeavy = false;

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
		if (attacker.IsRanged)
		{
			SpawnRoomProjectileEffect(attacker.Position + dir * 10f, target, damage, attacker.IsPlayerSide, heavy);
		}
		else
		{
			ApplyProjectileOrSlashHit(target, damage, dir, heavy, false);
			SpawnRoomMeleeArcEffect(attacker, attacker.IsPlayerSide, heavy);
		}
	}

	private void ApplyRoomHit(RoomUnit attacker, RoomUnit target, int damage, Vector2 dir, bool heavy)
	{
		target.Hp = Mathf.Max(0, target.Hp - damage);
		target.HitFlash = heavy ? 0.32f : 0.24f;
		target.StaggerTime = Mathf.Max(target.StaggerTime, attacker.IsRanged ? 0.08f : (heavy ? 0.16f : 0.12f));
		target.HitPauseTime = Mathf.Max(target.HitPauseTime, heavy ? 0.05f : 0.035f);
		attacker.HitPauseTime = Mathf.Max(attacker.HitPauseTime, heavy ? 0.04f : 0.025f);
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
	}

	private void ApplyProjectileOrSlashHit(RoomUnit target, int damage, Vector2 dir, bool heavy, bool rangedHit)
	{
		if (target == null || !target.IsAlive)
		{
			return;
		}

		target.Hp = Mathf.Max(0, target.Hp - damage);
		target.HitFlash = heavy ? 0.32f : 0.24f;
		target.StaggerTime = Mathf.Max(target.StaggerTime, rangedHit ? 0.1f : (heavy ? 0.16f : 0.12f));
		target.HitPauseTime = Mathf.Max(target.HitPauseTime, heavy ? 0.05f : 0.035f);
		ApplyRoomKnockback(target, dir, rangedHit, heavy);

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
			knockbackForce = heavy ? 240f : 170f;
			knockbackDuration = heavy ? 0.2f : 0.14f;
		}
		else
		{
			knockbackForce = heavy ? 880f : 640f;
			knockbackDuration = heavy ? 0.68f : 0.52f;
		}

		target.KnockbackVelocity = dir * knockbackForce;
		target.KnockbackTime = knockbackDuration;
	}

	private void HandleUnitDeath(RoomUnit dead)
	{
		if (!dead.IsPlayerSide)
		{
			MapNode node = _nodes[_playerNodeId];
			if (dead.IsElite)
			{
				LootContainer elite = new() { Label = dead.Name, Kind = ContainerKind.EliteCorpse };
				elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Weapon, Label = "精钢军刀" });
				elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Armor, Label = "队长锁甲" });
				elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Trinket, Label = "纹章坠饰" });
				elite.HiddenItems.Add(RollLootItem());
				node.Containers.Add(elite);
			}
			else
			{
				LootContainer pile = null;
				for (int i = node.Containers.Count - 1; i >= 0; i--)
				{
					if (node.Containers[i].Kind == ContainerKind.CorpsePile)
					{
						pile = node.Containers[i];
						break;
					}
				}

				if (pile == null)
				{
					pile = new LootContainer { Label = "尸体堆", Kind = ContainerKind.CorpsePile };
					node.Containers.Add(pile);
				}
				pile.HiddenItems.Add(RollLootItem());
			}

			if (!HasHostilesInRoom())
			{
				AiSquad squad = GetSquadAtNode(node.Id);
				if (squad != null)
				{
					squad.Intent = AiIntent.Defeated;
					squad.Strength = 0;
				}
				node.Threat = 0;
			}
		}
		else if (!dead.IsHero)
		{
			for (int i = _runSoldiers.Count - 1; i >= 0; i--)
			{
				if (_runSoldiers[i].Name == dead.Name)
				{
					_runSoldiers.RemoveAt(i);
					break;
				}
			}
		}
	}

	private void RelayoutAlliesAroundHero()
	{
		RoomUnit hero = FindHeroUnit();
		if (hero == null)
		{
			return;
		}

		int index = 0;
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsPlayerSide || unit.IsHero || !unit.IsAlive)
			{
				continue;
			}

			Vector2 offset = new Vector2(-24f - (index % 3) * 20f, (index / 3) * 24f - 24f);
			unit.Position = ClampToRoom(hero.Position + offset);
			index++;
		}
	}

	private RoomDoorSide GetExitSide(MapNode fromNode, int linkedNodeId)
	{
		if (linkedNodeId < 0 || linkedNodeId >= _nodes.Count)
		{
			return RoomDoorSide.Right;
		}

		Vector2 delta = _nodes[linkedNodeId].Position - fromNode.Position;
		if (Mathf.Abs(delta.X) >= Mathf.Abs(delta.Y))
		{
			return delta.X >= 0f ? RoomDoorSide.Right : RoomDoorSide.Left;
		}

		return delta.Y >= 0f ? RoomDoorSide.Bottom : RoomDoorSide.Top;
	}

	private RoomDoorSide GetExitSide(int index, int totalCount)
	{
		return totalCount switch
		{
			1 => RoomDoorSide.Right,
			2 => index == 0 ? RoomDoorSide.Left : RoomDoorSide.Right,
			3 => index switch
			{
				0 => RoomDoorSide.Left,
				1 => RoomDoorSide.Top,
				_ => RoomDoorSide.Right,
			},
			_ => index switch
			{
				0 => RoomDoorSide.Left,
				1 => RoomDoorSide.Top,
				2 => RoomDoorSide.Right,
				_ => RoomDoorSide.Bottom,
			},
		};
	}

	private RoomDoorSide GetOppositeSide(RoomDoorSide side)
	{
		return side switch
		{
			RoomDoorSide.Left => RoomDoorSide.Right,
			RoomDoorSide.Right => RoomDoorSide.Left,
			RoomDoorSide.Top => RoomDoorSide.Bottom,
			_ => RoomDoorSide.Top,
		};
	}

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

		DrawString(ThemeDB.FallbackFont, arena.Position + new Vector2(14f, 22f), node.Name, HorizontalAlignment.Left, -1f, 20, Colors.White);
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
		DrawRoomImpactEffects();
		DrawRoomUnits();
	}

	private void DrawRoomExitsUnified(MapNode node)
	{
		for (int i = 0; i < node.Links.Count && i < 4; i++)
		{
			int linkedNodeId = node.Links[i];
			MapNode linkedNode = _nodes[linkedNodeId];
			RoomDoorSide side = GetExitSide(node, linkedNodeId);
			Rect2 exitRect = GetRoomExitRect(side);
			bool pending = linkedNodeId == _pendingExitNodeId;
			Color fill = pending ? new Color(0.3f, 0.62f, 0.82f, 0.92f) : new Color(0.28f, 0.34f, 0.44f, 0.9f);
			Color border = pending ? new Color(1f, 0.92f, 0.58f, 1f) : new Color(0.85f, 0.92f, 1f, 0.96f);
			DrawRect(exitRect, fill, true);
			DrawRect(exitRect, border, false, 2f);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(8f, 17f), GetCleanExitDirectionLabel(side), HorizontalAlignment.Left, exitRect.Size.X - 16f, 12, Colors.White);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(8f, 34f), linkedNode.Name, HorizontalAlignment.Left, exitRect.Size.X - 16f, 11, new Color(0.9f, 0.95f, 1f));
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
				DrawCircle(unit.Position, 6f, new Color(0.42f, 0.42f, 0.46f, 0.7f));
				continue;
			}

			Color body = unit.IsPlayerSide
				? (unit.IsHero ? new Color(0.34f, 0.84f, 1f) : new Color(0.5f, 0.92f, 0.72f))
				: (unit.IsElite ? new Color(0.96f, 0.52f, 0.4f) : new Color(0.92f, 0.38f, 0.38f));
			if (unit.HitFlash > 0f)
			{
				float flash = Mathf.Clamp(unit.HitFlash * 5f, 0f, 1f);
				body = body.Lerp(Colors.White, flash);
			}

			float radius = unit.IsHero ? 12f : 9f;
			Vector2 dir = unit.Facing == Vector2.Zero ? (unit.IsPlayerSide ? Vector2.Right : Vector2.Left) : unit.Facing.Normalized();
			Vector2 normal = new(-dir.Y, dir.X);
			Vector2 tip = unit.Position + dir * (radius + 6f);
			Vector2 left = unit.Position - dir * radius + normal * (radius * 0.75f);
			Vector2 right = unit.Position - dir * radius - normal * (radius * 0.75f);
			DrawColoredPolygon(new Vector2[] { tip, left, right }, body);
			DrawPolyline(new Vector2[] { tip, left, right, tip }, Colors.White, 1.4f);
			if (unit.AttackWindupTime > 0f)
			{
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
			Rect2 hpBg = new(unit.Position + new Vector2(-20f, 12f), new Vector2(40f, 4f));
			DrawRect(hpBg, new Color(0.14f, 0.14f, 0.16f), true);
			float ratio = unit.MaxHp > 0 ? (float)unit.Hp / unit.MaxHp : 0f;
			DrawRect(new Rect2(hpBg.Position, new Vector2(hpBg.Size.X * ratio, hpBg.Size.Y)), unit.IsPlayerSide ? new Color(0.46f, 0.95f, 0.58f) : new Color(0.95f, 0.5f, 0.5f), true);
			DrawRect(hpBg, Colors.White, false, 1f);
			DrawString(ThemeDB.FallbackFont, unit.Position + new Vector2(-22f, -14f), unit.Name, HorizontalAlignment.Left, 80f, 10, Colors.White);
		}
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
				Vector2[] tip =
				[
					endPos,
					tipBase + tipNormal * tipWidth,
					tipBase - tipNormal * tipWidth,
				];
				DrawColoredPolygon(tip, new Color(1f, 1f, 1f, alpha * 0.92f));
			}
		}

	}

	private void TryMoveToNode(int nodeId)
	{
		MapNode current = _nodes[_playerNodeId];
		if (!current.Links.Contains(nodeId))
		{
			_status = "该节点与当前位置不相连。";
			return;
		}

		_isPlayerMoving = true;
		_plannedExitNodeId = nodeId;
		_moveTargetNodeId = nodeId;
		_moveProgress = 0f;
		_playerMarkerPosition = _nodes[_playerNodeId].Position;
		AdvanceTurn($"移动至 {_nodes[nodeId].Name}。");
		_status = $"正在前往 {_nodes[nodeId].Name}。";
	}

	private void TryPlanExitToNode(int nodeId)
	{
		MapNode current = _nodes[_playerNodeId];
		if (!current.Links.Contains(nodeId))
		{
			_status = "战略地图只能标记相邻节点。";
			return;
		}

		_plannedExitNodeId = nodeId;
		_status = $"已标记出口目标：{_nodes[nodeId].Name}。返回房间后可通过对应出口转场。";
	}

	private void TryUseExit(int nodeId)
	{
		if (_selectedContainerIndex >= 0 || _runEnded || _inHideout)
		{
			return;
		}

		MapNode node = _nodes[_playerNodeId];
		if (!node.Links.Contains(nodeId))
		{
			return;
		}

		RoomDoorSide side = GetExitSide(node, nodeId);
		Rect2 doorRect = GetRoomExitRect(side);
		_pendingExitNodeId = nodeId;
		_pendingExitSide = side;
		_heroMoveTarget = ClampToRoom(doorRect.GetCenter());
		_heroHasMoveTarget = true;
		_status = $"前往 {_nodes[nodeId].Name} 的门口。";
	}

	private void HandleArrival(MapNode node)
	{
		node.SearchRewardClaimed = true;
		_roomDirty = true;
		RefreshStatus();
		return;

		AiSquad encounteredSquad = GetSquadAtNode(node.Id);
		if (encounteredSquad != null)
		{
			QueueEncounter(node, encounteredSquad.Name, encounteredSquad.Strength + 8, encounteredSquad, true, $"你在 {node.Name} 遭遇了 {encounteredSquad.Name}。");
			return;
		}

		if (node.Threat > 0)
		{
			QueueEncounter(node, $"{node.Name}守军", node.Threat * 4 + 6, null, node.Type == NodeType.Battle, $"你进入了 {node.Name}，守军拦住了去路。");
			return;
		}

		AiSquad squad = GetSquadAtNode(node.Id);
		if (squad != null)
		{
			StartEncounter(node, squad.Name, squad.Strength + 8, squad, true);
			return;
		}

		if (node.Threat > 0)
		{
			StartEncounter(node, $"{node.Name}守军", node.Threat * 4 + 6, null, node.Type == NodeType.Battle);
			return;
		}

		node.SearchRewardClaimed = true;
		RefreshStatus();
	}

	private void StartEncounter(MapNode node, string enemyName, int enemyPower, AiSquad squad, bool hasElite)
	{
		_roomDirty = true;
		return;

		_encounter = new Encounter
		{
			EnemyName = enemyName,
			TurnCost = 1,
			EnemyPower = enemyPower,
			EnemyHasElite = hasElite,
			Node = node,
			Squad = squad,
		};

		_status = $"{node.Name} 爆发战斗。";
		_battleSim = new RoomBattleSim();
		AddChild(_battleSim);
		_battleSim.BattleFinished += OnBattleFinished;
		_battleSim.Setup(node.Name, _playerHp, _playerStrength, _runSoldiers.Count, enemyPower, hasElite, enemyName);
	}

	private void QueueEncounter(MapNode node, string enemyName, int enemyPower, AiSquad squad, bool hasElite, string promptText)
	{
		_roomDirty = true;
		return;

		if (_selectedContainerIndex >= 0)
		{
			_selectedContainerIndex = -1;
		}

		_pendingEncounter = new Encounter
		{
			EnemyName = enemyName,
			TurnCost = 1,
			EnemyPower = enemyPower,
			EnemyHasElite = hasElite,
			Node = node,
			Squad = squad,
			PromptText = promptText,
		};
		_status = promptText;
	}

	private void StartPendingEncounter()
	{
		_roomDirty = true;
		return;

		if (_pendingEncounter == null)
		{
			return;
		}

		Encounter pending = _pendingEncounter;
		_pendingEncounter = null;
		StartEncounter(pending.Node, pending.EnemyName, pending.EnemyPower, pending.Squad, pending.EnemyHasElite);
	}

	private void OnBattleFinished(bool victory, bool heroAlive, int remainingHp, int remainingSoldiers, int remainingStrength)
	{
		return;

		if (_battleSim != null)
		{
			_battleSim.BattleFinished -= OnBattleFinished;
			_battleSim.QueueFree();
			_battleSim = null;
		}

		if (_encounter == null)
		{
			return;
		}

		MapNode node = _encounter.Node;
		AiSquad squad = _encounter.Squad;
		AdvanceTurn(victory ? $"在 {node.Name} 取得胜利。" : $"在 {node.Name} 战败。", _encounter.TurnCost, false);
		ApplySoldierLosses(remainingSoldiers);

		if (!victory)
		{
			_playerHp = 0;
			_runBackpack.Clear();
			_runEnded = true;
			_runFailed = true;
			_status = "全队死亡，本局物资全部丢失。";
			LogEvent("玩家小队全灭，本局物资未能带出。");
			_encounter = null;
			return;
		}

		_playerHp = heroAlive ? Mathf.Clamp(remainingHp, 1, _playerMaxHp) : 1;
		_playerStrength = Mathf.Max(3 + _runSoldiers.Count, remainingStrength);
		node.Threat = 0;
		GenerateBattleLoot(node, squad);
		if (!heroAlive)
		{
			LogEvent("英雄在战斗中倒下，但幸存士兵赢得了战斗。英雄以 1 点生命值继续探索。");
		}

		if (squad != null)
		{
			squad.Intent = AiIntent.Defeated;
			squad.Strength = 0;
			LogEvent($"{squad.Name} 被玩家击溃。");
		}
		else
		{
			LogEvent($"{node.Name} 的守军被击败。");
		}

		_status = $"{node.Name} 战斗胜利，可以开始搜刮战利品。";
		_encounter = null;
	}

	private void GenerateBattleLoot(MapNode node, AiSquad squad)
	{
		LootContainer pile = new() { Label = "尸体堆", Kind = ContainerKind.CorpsePile };
		int count = _rng.RandiRange(3, 5);
		for (int i = 0; i < count; i++)
		{
			pile.HiddenItems.Add(squad != null && squad.Loot.Count > 0 ? TakeRandomLoot(squad) : RollLootItem());
		}
		node.Containers.Add(pile);

		LootContainer elite = new() { Label = squad != null ? $"{squad.Name} 队长" : "精英守卫", Kind = ContainerKind.EliteCorpse };
		elite.VisibleItems.Add(squad != null ? "精钢军刀" : "守卫长枪");
		elite.VisibleItems.Add(squad != null ? "队长锁甲" : "钢片甲");
		elite.HiddenItems.Add("绷带包");
		elite.HiddenItems.Add(squad != null && squad.Loot.Count > 0 ? TakeRandomLoot(squad) : RollLootItem());
		PromoteEliteEquipment(elite);
		node.Containers.Add(elite);
	}

	private string TakeRandomLoot(AiSquad squad)
	{
		int index = _rng.RandiRange(0, squad.Loot.Count - 1);
		string item = squad.Loot[index];
		squad.Loot.RemoveAt(index);
		return item;
	}

	private void PromoteEliteEquipment(LootContainer elite)
	{
		if (elite.Kind != ContainerKind.EliteCorpse)
		{
			return;
		}

		if (elite.VisibleItems.Count > 0)
		{
			elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Weapon, Label = elite.VisibleItems[0] });
		}
		if (elite.VisibleItems.Count > 1)
		{
			elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Armor, Label = elite.VisibleItems[1] });
		}
		if (elite.EquippedItems.Count < 3)
		{
			elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Trinket, Label = "绶带坠饰" });
		}

		elite.VisibleItems.Clear();
	}

	private void EnsureContainerGrid(LootContainer container)
	{
		if (container.GridItems.Count > 0)
		{
			return;
		}

		container.GridSize = GetContainerGridSize(container);
		foreach (string item in container.VisibleItems)
		{
			AddGridItem(container, item, true);
		}

		for (int i = container.HiddenIndex; i < container.HiddenItems.Count; i++)
		{
			AddGridItem(container, container.HiddenItems[i], false);
		}

		container.VisibleItems.Clear();
		container.HiddenItems.Clear();
		container.HiddenIndex = 0;
	}

	private Vector2I GetContainerGridSize(LootContainer container) => container.Kind switch
	{
		ContainerKind.Room => new Vector2I(5, 4),
		ContainerKind.CorpsePile => new Vector2I(6, 5),
		ContainerKind.EliteCorpse => new Vector2I(5, 4),
		_ => new Vector2I(5, 4),
	};

	private void AddGridItem(LootContainer container, string label, bool revealed)
	{
		GridLootItem item = CreateGridLootItem(label, revealed);
		if (TryPlaceGridItem(container, item))
		{
			container.GridItems.Add(item);
		}
	}

	private GridLootItem CreateGridLootItem(string label, bool revealed)
	{
		ItemRarity rarity = RollGridRarity(label);
		Vector2I size = rarity switch
		{
			ItemRarity.White => _rng.Randf() < 0.6f ? new Vector2I(1, 1) : new Vector2I(1, 2),
			ItemRarity.Green => _rng.Randf() < 0.5f ? new Vector2I(1, 2) : new Vector2I(2, 1),
			ItemRarity.Blue => _rng.Randf() < 0.5f ? new Vector2I(2, 2) : new Vector2I(1, 3),
			ItemRarity.Purple => new Vector2I(2, 2),
			ItemRarity.Gold => new Vector2I(2, 3),
			_ => Vector2I.One,
		};

		return new GridLootItem
		{
			Label = label,
			Rarity = rarity,
			Size = size,
			Revealed = revealed,
			SearchTime = GetGridSearchTime(rarity),
		};
	}

	private ItemRarity RollGridRarity(string label)
	{
		if (label.Contains("遗物") || label.Contains("宝石")) return ItemRarity.Gold;
		if (label.Contains("军刀") || label.Contains("锁甲") || label.Contains("长枪")) return ItemRarity.Blue;
		if (label.Contains("坠饰") || label.Contains("徽记")) return ItemRarity.Purple;
		if (label.Contains("口粮") || label.Contains("药")) return ItemRarity.Green;
		return ItemRarity.White;
	}

	private float GetGridSearchTime(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 0.8f,
		ItemRarity.Green => 1.15f,
		ItemRarity.Blue => 1.6f,
		ItemRarity.Purple => 2.15f,
		ItemRarity.Gold => 2.9f,
		_ => 1f,
	};

	private int GetTimeSlotCost(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 10,
		ItemRarity.Green => 20,
		ItemRarity.Blue => 30,
		ItemRarity.Purple => 40,
		ItemRarity.Gold => 50,
		_ => 10,
	};

	private bool TryPlaceGridItem(LootContainer container, GridLootItem item)
	{
		for (int y = 0; y <= container.GridSize.Y - item.Size.Y; y++)
		{
			for (int x = 0; x <= container.GridSize.X - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsGridAreaFree(container, cell, item.Size))
				{
					continue;
				}

				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsGridAreaFree(LootContainer container, Vector2I cell, Vector2I size)
	{
		foreach (GridLootItem existing in container.GridItems)
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

	private void UpdateContainerSearch(float delta)
	{
		if (_selectedContainerIndex < 0)
		{
			return;
		}

		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex < 0 || _selectedContainerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[_selectedContainerIndex];
		if (container.ActiveSearchItemIndex < 0 || container.ActiveSearchItemIndex >= container.GridItems.Count)
		{
			return;
		}

		GridLootItem item = container.GridItems[container.ActiveSearchItemIndex];
		item.SearchProgress += delta;
		if (item.SearchProgress < item.SearchTime)
		{
			return;
		}

		item.Revealed = true;
		item.SearchProgress = item.SearchTime;
		container.ActiveSearchItemIndex = -1;
		AddTimeSlotProgress(GetTimeSlotCost(item.Rarity), item.Label);
		LogEvent($"搜索完成，发现了 {item.Label}。");
		if (_autoSearchEnabled)
		{
			StartSearchOnNextHiddenItem(_selectedContainerIndex);
		}
	}

	private void StartSearchOnNextHiddenItem(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		EnsureContainerGrid(container);
		if (container.ActiveSearchItemIndex >= 0)
		{
			return;
		}

		for (int i = 0; i < container.GridItems.Count; i++)
		{
			GridLootItem item = container.GridItems[i];
			if (item.Revealed || item.Taken)
			{
				continue;
			}

			TrySearch(containerIndex * 100 + i);
			return;
		}
	}

	private void AddTimeSlotProgress(int amount, string itemLabel)
	{
		_timeSlotProgress += Mathf.Max(0, amount);
		if (_timeSlotProgress < 100)
		{
			RefreshStatus();
			return;
		}

		int extraTurns = _timeSlotProgress / 100;
		_timeSlotProgress %= 100;
		AdvanceTurn($"搜索 {itemLabel} 耗费了额外时间。", extraTurns, false);
		RefreshStatus();
	}

	private void AdvanceTurn(string reason, int amount = 1, bool refreshStatus = true)
	{
		for (int i = 0; i < amount; i++)
		{
			_turn++;
			SimulateAiTurn();
			CheckPlayerNodeEncounterAfterTimeAdvance();
		}

		LogEvent($"第 {_turn} 回合：{reason}");
		if (refreshStatus && !_runEnded)
		{
			RefreshStatus();
		}
	}

	private void SimulateAiTurn()
	{
		ResolveAiMeetings();
		foreach (AiSquad squad in _aiSquads)
		{
			if (!squad.IsAlive)
			{
				continue;
			}

			if (squad.BusyTurns > 0)
			{
				squad.BusyTurns--;
				if (squad.BusyTurns == 0)
				{
					ResolveAiBusyAction(squad);
				}
				continue;
			}

			if (squad.Strength <= 3 || squad.Supplies <= 0 || squad.Loot.Count >= 4)
			{
				int extractId = FindExtractNode();
				if (squad.NodeId == extractId)
				{
					squad.Intent = AiIntent.Extracted;
					squad.IntentTargetNodeId = extractId;
					LogEvent($"{squad.Name} 已撤离，带走了 {squad.Loot.Count} 件战利品。");
				}
				else
				{
					MoveAiToward(squad, extractId, AiIntent.Extracting);
				}
				continue;
			}

			MapNode node = _nodes[squad.NodeId];
			if (node.Threat > 0)
			{
				squad.Intent = AiIntent.Clearing;
				squad.IntentTargetNodeId = node.Id;
				squad.BusyTurns = _rng.RandiRange(1, 2);
				LogEvent($"{squad.Name} 开始清理 {node.Name}。");
				continue;
			}

			if (CanAiLootNode(node))
			{
				squad.Intent = AiIntent.Looting;
				squad.IntentTargetNodeId = node.Id;
				squad.BusyTurns = 1;
				LogEvent($"{squad.Name} 开始搜刮 {node.Name}。");
				continue;
			}

			MoveAiToward(squad, PickAiTargetNode(squad), AiIntent.Moving);
		}
		ResolveAiMeetings();
	}

	private void ResolveAiBusyAction(AiSquad squad)
	{
		MapNode node = _nodes[squad.NodeId];
		if (squad.Intent == AiIntent.Clearing)
		{
			int loss = _rng.RandiRange(0, 2);
			squad.Strength = Mathf.Max(1, squad.Strength - loss);
			node.Threat = 0;
			LogEvent($"{squad.Name} 清理了 {node.Name}，损失了 {loss} 点战力。");
		}
		else if (squad.Intent == AiIntent.Looting)
		{
			if (TryAiLootNode(squad, node))
			{
				LogEvent($"{squad.Name} 搜刮了 {node.Name}。");
			}
		}
		else if (squad.Intent == AiIntent.Fighting)
		{
			ResolveAiDuel(squad);
			return;
		}

		squad.Intent = AiIntent.Idle;
		squad.IntentTargetNodeId = -1;
	}

	private void ResolveAiMeetings()
	{
		for (int i = 0; i < _aiSquads.Count; i++)
		{
			AiSquad a = _aiSquads[i];
			if (!a.IsAlive || a.BusyTurns > 0 || a.NodeId == _playerNodeId)
			{
				continue;
			}

			for (int j = i + 1; j < _aiSquads.Count; j++)
			{
				AiSquad b = _aiSquads[j];
				if (!b.IsAlive || b.BusyTurns > 0 || b.NodeId != a.NodeId)
				{
					continue;
				}

				a.Intent = AiIntent.Fighting;
				b.Intent = AiIntent.Fighting;
				a.IntentTargetNodeId = a.NodeId;
				b.IntentTargetNodeId = b.NodeId;
				a.BusyTurns = _rng.RandiRange(3, 5);
				b.BusyTurns = a.BusyTurns;
				a.RivalId = j;
				b.RivalId = i;
				LogEvent($"{a.Name} 与 {b.Name} 在 {_nodes[a.NodeId].Name} 交战。");
			}
		}
	}

	private void CheckPlayerNodeEncounterAfterTimeAdvance()
	{
		if (_runEnded || _inHideout)
		{
			return;
		}

		_roomDirty = true;
	}

	private void ResolveAiDuel(AiSquad squad)
	{
		if (squad.RivalId < 0 || squad.RivalId >= _aiSquads.Count)
		{
			squad.Intent = AiIntent.Idle;
			squad.IntentTargetNodeId = -1;
			return;
		}

		AiSquad rival = _aiSquads[squad.RivalId];
		if (!rival.IsAlive)
		{
			squad.Intent = AiIntent.Idle;
			squad.IntentTargetNodeId = -1;
			squad.RivalId = -1;
			return;
		}

		if (_aiSquads.IndexOf(squad) > squad.RivalId)
		{
			return;
		}

		AiSquad winner = squad.Strength + _rng.RandiRange(0, 4) >= rival.Strength + _rng.RandiRange(0, 4) ? squad : rival;
		AiSquad loser = winner == squad ? rival : squad;
		winner.Strength = Mathf.Max(1, winner.Strength - _rng.RandiRange(1, 3));
		winner.Intent = AiIntent.Idle;
		winner.IntentTargetNodeId = -1;
		winner.BusyTurns = 0;
		winner.RivalId = -1;
		loser.Strength = 0;
		loser.Intent = AiIntent.Defeated;
		loser.IntentTargetNodeId = -1;
		loser.BusyTurns = 0;
		loser.RivalId = -1;
		MapNode node = _nodes[winner.NodeId];
		LootContainer pile = new() { Label = $"{loser.Name} 的遗骸", Kind = ContainerKind.CorpsePile };
		pile.HiddenItems.Add("破损徽章");
		pile.HiddenItems.Add("野战口粮");
		pile.HiddenItems.Add(RollLootItem());
		node.Containers.Add(pile);
		LogEvent($"{winner.Name} 在 {node.Name} 击败了 {loser.Name}。");
	}

	private bool CanAiLootNode(MapNode node)
	{
		if (node.Id == _playerNodeId || node.Threat > 0)
		{
			return false;
		}

		foreach (LootContainer container in node.Containers)
		{
			if (!container.IsEmpty)
			{
				return true;
			}
		}

		return false;
	}

	private bool TryAiLootNode(AiSquad squad, MapNode node)
	{
		foreach (LootContainer container in node.Containers)
		{
			foreach (EquippedLoot equipped in container.EquippedItems)
			{
				if (equipped.Taken || string.IsNullOrEmpty(equipped.Label))
				{
					continue;
				}

				equipped.Taken = true;
				squad.Loot.Add(equipped.Label);
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}

			if (container.VisibleItems.Count > 0)
			{
				squad.Loot.Add(container.VisibleItems[0]);
				container.VisibleItems.RemoveAt(0);
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}

			if (container.HiddenRemaining > 0)
			{
				squad.Loot.Add(container.HiddenItems[container.HiddenIndex]);
				container.HiddenIndex++;
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}
		}
		return false;
	}

	private int PickAiTargetNode(AiSquad squad)
	{
		int bestId = squad.NodeId;
		float bestScore = float.MinValue;
		foreach (MapNode node in _nodes)
		{
			if (node.Id == squad.NodeId)
			{
				continue;
			}

			float score = 0f;
			score += node.Type == NodeType.Search ? 6f : 0f;
			score += node.Type == NodeType.Battle ? 3f : 0f;
			score += node.Threat > 0 ? 3f : 0f;
			score += CountNodeLoot(node) * 1.3f;
			score -= _nodes[squad.NodeId].Position.DistanceTo(node.Position) * 0.01f;
			if (node.Id == _playerNodeId)
			{
				score += 1.5f;
			}

			if (score > bestScore)
			{
				bestScore = score;
				bestId = node.Id;
			}
		}
		return bestId;
	}

	private void MoveAiToward(AiSquad squad, int targetId, AiIntent intent)
	{
		int nextId = FindNextStep(squad.NodeId, targetId);
		if (nextId == squad.NodeId)
		{
			squad.IntentTargetNodeId = targetId;
			return;
		}
		squad.NodeId = nextId;
		squad.Intent = intent;
		squad.IntentTargetNodeId = targetId;
		squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
		LogEvent($"{squad.Name} 移动到了 {_nodes[nextId].Name}。");
	}

	private int FindNextStep(int fromId, int targetId)
	{
		if (fromId == targetId)
		{
			return fromId;
		}

		int best = fromId;
		float bestDistance = float.MaxValue;
		foreach (int link in _nodes[fromId].Links)
		{
			float distance = _nodes[link].Position.DistanceTo(_nodes[targetId].Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = link;
			}
		}
		return best;
	}

	private int FindExtractNode()
	{
		foreach (MapNode node in _nodes)
		{
			if (node.Type == NodeType.Extract)
			{
				return node.Id;
			}
		}
		return 0;
	}

	private AiSquad GetSquadAtNode(int nodeId)
	{
		foreach (AiSquad squad in _aiSquads)
		{
			if (squad.IsAlive && squad.NodeId == nodeId)
			{
				return squad;
			}
		}
		return null;
	}

	private void HandleButton(ButtonDef button)
	{
		if (_inHideout)
		{
			switch (button.Action)
			{
				case "start_run":
					ResetRun();
					return;
				case "sell_stash":
					SellStashItem(button.Index);
					return;
				case "buy_shop":
					BuyShopItem(button.Index);
					return;
				case "recruit_soldier":
					RecruitSoldier();
					return;
				case "select_map_prev":
					_selectedMapTemplate = (_selectedMapTemplate + MapTemplateCount - 1) % MapTemplateCount;
					ClampSelectedDifficulty();
					return;
				case "select_map_next":
					_selectedMapTemplate = (_selectedMapTemplate + 1) % MapTemplateCount;
					ClampSelectedDifficulty();
					return;
				case "select_diff_prev":
					CycleDifficulty(-1);
					return;
				case "select_diff_next":
					CycleDifficulty(1);
					return;
			}
		}

		if (_runEnded)
		{
			if (button.Action == "restart")
			{
				InitHideout();
			}
			return;
		}

		switch (button.Action)
		{
			case "toggle_map":
				ToggleMapOverlay();
				break;
			case "use_exit":
				TryUseExit(button.Index);
				break;
			case "search":
				TrySearch(button.Index);
				break;
			case "take":
				TakeVisible(button.Index);
				break;
			case "open_container":
				OpenContainer(button.Index);
				break;
			case "close_container":
				_selectedContainerIndex = -1;
				RefreshStatus();
				break;
			case "extract":
				TryExtract();
				break;
			case "toggle_auto_search":
				_autoSearchEnabled = !_autoSearchEnabled;
				_status = _autoSearchEnabled ? "已开启自动搜索。打开容器时会自动揭示物品。" : "已关闭自动搜索。";
				break;
			case "encounter_fight":
				StartPendingEncounter();
				break;
			case "confirm_search_yes":
			case "confirm_search_no":
			case "toggle_confirm_skip":
				break;
		}
	}

	private void TrySearch(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "当前房间不够安全，无法搜索。";
			return;
		}

		int containerIndex = encodedIndex / 100;
		int itemIndex = encodedIndex % 100;
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		EnsureContainerGrid(container);
		if (itemIndex < 0 || itemIndex >= container.GridItems.Count)
		{
			return;
		}

		GridLootItem item = container.GridItems[itemIndex];
		if (item.Revealed || item.Taken)
		{
			_status = "这个容器里没有未检视物品了。";
			return;
		}

		item.SearchProgress = 0f;
		container.ActiveSearchItemIndex = itemIndex;
		LogEvent($"开始搜索 {container.Label} 中的物品。");
		RefreshStatus();
	}

	private void OpenContainer(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		_selectedContainerIndex = containerIndex;
		EnsureContainerGrid(node.Containers[containerIndex]);
		if (_autoSearchEnabled && CanSearch(node))
		{
			StartSearchOnNextHiddenItem(containerIndex);
			return;
		}

		RefreshStatus();
	}

	private void ConfirmSearchExchange(bool confirmed)
	{
		_showSearchConfirm = false;
		if (!confirmed)
		{
			_pendingRevealContainerIndex = -1;
			_status = "已取消兑换搜索次数。";
			return;
		}

		if (_confirmSkipChecked)
		{
			_skipSearchConfirm = true;
		}

		if (_pendingRevealContainerIndex >= 0)
		{
			int targetIndex = _pendingRevealContainerIndex;
			_pendingRevealContainerIndex = -1;
			if (_searchActions <= 0)
			{
				AdvanceTurn($"在 {_nodes[_playerNodeId].Name} 花费时间搜索。", 1, false);
				_searchActions += 4;
			}
			TrySearch(targetIndex);
		}
	}

	private void TakeVisible(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "房间仍然危险，暂时不能搜刮。";
			return;
		}

		int containerIndex = encodedIndex / 100;
		int itemIndex = encodedIndex % 100;
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}
		LootContainer container = node.Containers[containerIndex];
		if (itemIndex >= 50)
		{
			int equipIndex = itemIndex - 50;
			if (equipIndex < 0 || equipIndex >= container.EquippedItems.Count)
			{
				return;
			}

			EquippedLoot equipped = container.EquippedItems[equipIndex];
			if (equipped.Taken || string.IsNullOrEmpty(equipped.Label))
			{
				return;
			}

			equipped.Taken = true;
			AddLoot(equipped.Label);
			LogEvent($"从 {container.Label} 取走了 {equipped.Label}。");
			RefreshStatus();
			return;
		}

		if (container.GridItems.Count > 0)
		{
			if (itemIndex < 0 || itemIndex >= container.GridItems.Count)
			{
				return;
			}

			GridLootItem gridItem = container.GridItems[itemIndex];
			if (!gridItem.Revealed || gridItem.Taken)
			{
				return;
			}

			gridItem.Taken = true;
			AddLoot(gridItem.Label);
			LogEvent($"从 {container.Label} 取走了 {gridItem.Label}。");
			RefreshStatus();
			return;
		}

		if (itemIndex < 0 || itemIndex >= container.VisibleItems.Count)
		{
			return;
		}

		string item = container.VisibleItems[itemIndex];
		container.VisibleItems.RemoveAt(itemIndex);
		AddLoot(item);
		LogEvent($"从 {container.Label} 取走了 {item}。");
		RefreshStatus();
	}

	private void BuySearchActions()
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "这里无法安心停下来搜索。";
			return;
		}

		AdvanceTurn($"在 {node.Name} 花费时间搜索。");
		_searchActions += 4;
		_status = $"在 {node.Name} 获得了 4 次搜索机会。";
	}

	private void TryExtract()
	{
		if (_nodes[_playerNodeId].Type != NodeType.Extract)
		{
			_status = "当前位置不是撤离点。";
			return;
		}

		foreach (string item in _runBackpack)
		{
			_stash.Add(item);
		}
		_runEnded = true;
		_runFailed = false;
		_status = "撤离成功。";
		LogEvent("玩家成功撤离。");
	}

	private bool CanSearch(MapNode node)
	{
		return !_runEnded && !_inHideout;
	}

	private void AddLoot(string item)
	{
		_runBackpack.Add(item);
		_lootValue += GetItemValue(item);
	}

	private int GetItemValue(string item)
	{
		if (item.Contains("遗物") || item.Contains("徽记") || item.Contains("宝石")) return 18;
		if (item.Contains("锁甲") || item.Contains("军刀") || item.Contains("长枪")) return 12;
		if (item.Contains("包") || item.Contains("口粮")) return 7;
		return 5;
	}

	private string GetSelectedMapName() => _selectedMapTemplate switch
	{
		1 => "边境堡寨",
		_ => "沦陷修道院",
	};

	private string GetSelectedMapRoleLabel() => _selectedMapTemplate switch
	{
		1 => "进阶图",
		_ => "新手图",
	};

	private string GetDifficultyName(OperationDifficulty difficulty) => difficulty switch
	{
		OperationDifficulty.Trial => "试锋",
		OperationDifficulty.Muster => "整军",
		OperationDifficulty.AllIn => "倾巢",
		_ => "试锋",
	};

	private int GetDifficultyRequirement(OperationDifficulty difficulty) => difficulty switch
	{
		OperationDifficulty.Trial => 0,
		OperationDifficulty.Muster => 150000,
		OperationDifficulty.AllIn => 500000,
		_ => 0,
	};

	private float GetDifficultyThreatScale(OperationDifficulty difficulty) => difficulty switch
	{
		OperationDifficulty.Trial => 1f,
		OperationDifficulty.Muster => 1.45f,
		OperationDifficulty.AllIn => 2.1f,
		_ => 1f,
	};

	private float GetDifficultyLootScale(OperationDifficulty difficulty) => difficulty switch
	{
		OperationDifficulty.Trial => 1f,
		OperationDifficulty.Muster => 2f,
		OperationDifficulty.AllIn => 4f,
		_ => 1f,
	};

	private bool IsDifficultyAllowed(OperationDifficulty difficulty)
	{
		return _selectedMapTemplate switch
		{
			0 => difficulty is OperationDifficulty.Trial or OperationDifficulty.Muster,
			1 => difficulty is OperationDifficulty.Muster or OperationDifficulty.AllIn,
			_ => difficulty == OperationDifficulty.Trial,
		};
	}

	private void ClampSelectedDifficulty()
	{
		if (IsDifficultyAllowed(_selectedDifficulty))
		{
			return;
		}

		_selectedDifficulty = _selectedMapTemplate == 0 ? OperationDifficulty.Trial : OperationDifficulty.Muster;
	}

	private void CycleDifficulty(int direction)
	{
		int start = (int)_selectedDifficulty;
		int index = start;
		for (int i = 0; i < DifficultyCount; i++)
		{
			index = (index + direction + DifficultyCount) % DifficultyCount;
			OperationDifficulty candidate = (OperationDifficulty)index;
			if (!IsDifficultyAllowed(candidate))
			{
				continue;
			}

			_selectedDifficulty = candidate;
			return;
		}
	}

	private void SellStashItem(int index)
	{
		if (index < 0 || index >= _stash.Count)
		{
			return;
		}

		string item = _stash[index];
		_money += GetItemValue(item);
		_stash.RemoveAt(index);
	}

	private void BuyShopItem(int index)
	{
		if (index < 0 || index >= _shopStock.Count)
		{
			return;
		}

		ShopEntry entry = _shopStock[index];
		if (_money < entry.Price)
		{
			return;
		}

		_money -= entry.Price;
		_stash.Add(entry.Label);
	}

	private void RecruitSoldierInternal()
	{
		_soldierRoster.Add(new SoldierRecord { Name = $"士兵{_nextSoldierId}" });
		_nextSoldierId++;
	}

	private void RecruitSoldier()
	{
		if (_money < RecruitCost)
		{
			_status = "资金不足，无法征募新兵。";
			return;
		}

		_money -= RecruitCost;
		RecruitSoldierInternal();
		_status = $"已征募新兵，当前士兵数：{_soldierRoster.Count}。";
	}

	private void ApplySoldierLosses(int remainingSoldiers)
	{
		remainingSoldiers = Mathf.Clamp(remainingSoldiers, 0, _runSoldiers.Count);
		int losses = _runSoldiers.Count - remainingSoldiers;
		if (losses <= 0)
		{
			return;
		}

		for (int i = 0; i < losses; i++)
		{
			int runIndex = _runSoldiers.Count - 1;
			if (runIndex < 0)
			{
				break;
			}

			string name = _runSoldiers[runIndex].Name;
			_runSoldiers.RemoveAt(runIndex);
			for (int rosterIndex = _soldierRoster.Count - 1; rosterIndex >= 0; rosterIndex--)
			{
				if (_soldierRoster[rosterIndex].Name != name)
				{
					continue;
				}

				_soldierRoster.RemoveAt(rosterIndex);
				break;
			}

			LogEvent($"{name} 阵亡。");
		}
	}

	private string RollLootItem()
	{
		string[] items =
		[
			"古旧遗物", "银质宝石", "口粮包", "草药包", "封印徽记",
			"贸易账本", "生锈钥匙", "灯油", "细布卷"
		];
		return items[_rng.RandiRange(0, items.Length - 1)];
	}

	private string RollVisibleEquipment()
	{
		string[] items = ["佣兵长剑", "皮甲背心", "猎弓", "塔盾", "钢盔"];
		return items[_rng.RandiRange(0, items.Length - 1)];
	}

	private int CountNodeLoot(MapNode node)
	{
		int count = 0;
		foreach (LootContainer container in node.Containers)
		{
			foreach (EquippedLoot equipped in container.EquippedItems)
			{
				if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
				{
					count++;
				}
			}

			if (container.GridItems.Count > 0)
			{
				foreach (GridLootItem item in container.GridItems)
				{
					if (!item.Taken)
					{
						count++;
					}
				}
			}
			else
			{
				count += container.VisibleItems.Count + container.HiddenRemaining;
			}
		}
		return count;
	}

	private void RefreshStatus()
	{
		MapNode node = _nodes[_playerNodeId];
		AiSquad enemy = GetSquadAtNode(node.Id);
		if (enemy != null)
		{
			_status = $"当前节点存在敌对小队：{enemy.Name}。";
			return;
		}
		if (node.Threat > 0)
		{
			_status = $"{node.Name} 仍有守军驻守。";
			return;
		}
		if (node.Type == NodeType.Extract)
		{
			_status = "这里是撤离点。";
			return;
		}
		_status = $"{node.Name} 当前安全，时隙 {_timeSlotProgress}/100。";
	}

	private void ToggleMapOverlay()
	{
		if (_inHideout || _runEnded)
		{
			return;
		}

		_showMapOverlay = !_showMapOverlay;
		if (_showMapOverlay)
		{
			_status = "战略地图已展开。点击相邻节点规划出口，移动在房间内执行。";
		}
		else
		{
			RefreshStatus();
		}
	}

	private void LogEvent(string text)
	{
		_eventLog.Add(text);
		if (_eventLog.Count > 12)
		{
			_eventLog.RemoveAt(0);
		}
	}

	private void DrawHideout()
	{
		Vector2 viewport = GetViewportRect().Size;
		Vector2 panelSize = new(
			Mathf.Clamp(viewport.X - 120f, 1040f, 1420f),
			Mathf.Clamp(viewport.Y - 100f, 620f, 860f));
		Rect2 panel = new((viewport - panelSize) * 0.5f, panelSize);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.96f), true);
		DrawRect(panel, Colors.White, false, 2f);

		float x = panel.Position.X + 22f;
		float y = panel.Position.Y + 34f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "局外整备", HorizontalAlignment.Left, -1f, 26, Colors.White);
		y += 34f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"资金：{_money}", HorizontalAlignment.Left, -1f, 18, new Color(0.95f, 0.86f, 0.48f));
		y += 26f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"可用士兵：{_soldierRoster.Count}", HorizontalAlignment.Left, -1f, 16, new Color(0.76f, 0.9f, 0.82f));
		y += 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "行动地图", HorizontalAlignment.Left, -1f, 16, Colors.White);
		Rect2 mapPrevRect = new(new Vector2(x + 110f, y - 18f), new Vector2(28f, 24f));
		Rect2 mapNextRect = new(new Vector2(x + 400f, y - 18f), new Vector2(28f, 24f));
		Rect2 mapNameRect = new(new Vector2(x + 148f, y - 18f), new Vector2(242f, 24f));
		DrawButton(mapPrevRect, "<", new Color(0.22f, 0.24f, 0.29f));
		DrawRect(mapNameRect, new Color(0.11f, 0.12f, 0.15f), true);
		DrawRect(mapNameRect, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawString(ThemeDB.FallbackFont, mapNameRect.Position + new Vector2(10f, 17f), GetSelectedMapName(), HorizontalAlignment.Left, -1f, 13, new Color(0.88f, 0.9f, 0.95f));
		DrawButton(mapNextRect, ">", new Color(0.22f, 0.24f, 0.29f));
		_buttons.Add(new ButtonDef(mapPrevRect, "select_map_prev"));
		_buttons.Add(new ButtonDef(mapNextRect, "select_map_next"));
		y += 32f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"地图定位：{GetSelectedMapRoleLabel()}", HorizontalAlignment.Left, -1f, 14, new Color(0.76f, 0.84f, 0.94f));
		y += 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "行动难度", HorizontalAlignment.Left, -1f, 16, Colors.White);
		Rect2 diffPrevRect = new(new Vector2(x + 110f, y - 18f), new Vector2(28f, 24f));
		Rect2 diffNextRect = new(new Vector2(x + 400f, y - 18f), new Vector2(28f, 24f));
		Rect2 diffNameRect = new(new Vector2(x + 148f, y - 18f), new Vector2(242f, 24f));
		DrawButton(diffPrevRect, "<", new Color(0.22f, 0.24f, 0.29f));
		DrawRect(diffNameRect, new Color(0.11f, 0.12f, 0.15f), true);
		DrawRect(diffNameRect, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawString(ThemeDB.FallbackFont, diffNameRect.Position + new Vector2(10f, 17f), GetDifficultyName(_selectedDifficulty), HorizontalAlignment.Left, -1f, 13, new Color(0.96f, 0.9f, 0.78f));
		DrawButton(diffNextRect, ">", new Color(0.22f, 0.24f, 0.29f));
		_buttons.Add(new ButtonDef(diffPrevRect, "select_diff_prev"));
		_buttons.Add(new ButtonDef(diffNextRect, "select_diff_next"));
		y += 30f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"武备要求：{GetDifficultyRequirement(_selectedDifficulty)}", HorizontalAlignment.Left, -1f, 14, new Color(0.95f, 0.86f, 0.48f));

		Rect2 startRect = new(new Vector2(panel.End.X - 170f, panel.Position.Y + 24f), new Vector2(140f, 34f));
		DrawButton(startRect, "入局", new Color(0.24f, 0.62f, 0.36f));
		_buttons.Add(new ButtonDef(startRect, "start_run"));

		Rect2 recruitRect = new(new Vector2(panel.End.X - 170f, panel.Position.Y + 66f), new Vector2(140f, 30f));
		DrawButton(recruitRect, $"征募 {RecruitCost}", _money >= RecruitCost ? new Color(0.48f, 0.34f, 0.18f) : new Color(0.24f, 0.24f, 0.28f));
		if (_money >= RecruitCost)
		{
			_buttons.Add(new ButtonDef(recruitRect, "recruit_soldier"));
		}

		float contentTop = panel.Position.Y + 192f;
		float contentGap = 28f;
		float contentWidth = (panel.Size.X - 18f - 18f - contentGap) * 0.5f;
		float contentHeight = Mathf.Max(316f, panel.Size.Y - 304f);
		Rect2 stashRect = new(new Vector2(panel.Position.X + 18f, contentTop), new Vector2(contentWidth, contentHeight));
		Rect2 shopRect = new(new Vector2(stashRect.End.X + contentGap, contentTop), new Vector2(contentWidth, contentHeight));
		DrawRect(stashRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(shopRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(stashRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawRect(shopRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, stashRect.Position + new Vector2(14f, 24f), "仓库", HorizontalAlignment.Left, -1f, 18, Colors.White);
		DrawString(ThemeDB.FallbackFont, shopRect.Position + new Vector2(14f, 24f), "商店", HorizontalAlignment.Left, -1f, 18, Colors.White);

		float stashY = stashRect.Position.Y + 44f;
		if (_stash.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(stashRect.Position.X + 14f, stashY), "仓库为空", HorizontalAlignment.Left, -1f, 14, new Color(0.72f, 0.76f, 0.82f));
		}
		for (int i = 0; i < _stash.Count; i++)
		{
			Rect2 row = new(new Vector2(stashRect.Position.X + 14f, stashY), new Vector2(stashRect.Size.X - 28f, 28f));
			DrawRect(row, new Color(0.12f, 0.13f, 0.16f), true);
			DrawRect(row, new Color(0.34f, 0.37f, 0.42f), false, 1f);
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(10f, 19f), _stash[i], HorizontalAlignment.Left, 220f, 12, Colors.White);
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(250f, 19f), $"售价 {GetItemValue(_stash[i])}", HorizontalAlignment.Left, 70f, 12, new Color(0.95f, 0.86f, 0.48f));
			Rect2 sellRect = new(new Vector2(row.End.X - 68f, row.Position.Y + 2f), new Vector2(56f, 24f));
			DrawButton(sellRect, "出售", new Color(0.46f, 0.25f, 0.19f));
			_buttons.Add(new ButtonDef(sellRect, "sell_stash", i));
			stashY += 34f;
			if (stashY > stashRect.End.Y - 34f) break;
		}

		float shopY = shopRect.Position.Y + 44f;
		for (int i = 0; i < _shopStock.Count; i++)
		{
			ShopEntry entry = _shopStock[i];
			Rect2 row = new(new Vector2(shopRect.Position.X + 14f, shopY), new Vector2(shopRect.Size.X - 28f, 28f));
			DrawRect(row, new Color(0.12f, 0.13f, 0.16f), true);
			DrawRect(row, new Color(0.34f, 0.37f, 0.42f), false, 1f);
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(10f, 19f), entry.Label, HorizontalAlignment.Left, 220f, 12, Colors.White);
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(250f, 19f), $"价格 {entry.Price}", HorizontalAlignment.Left, 70f, 12, new Color(0.78f, 0.87f, 0.98f));
			Rect2 buyRect = new(new Vector2(row.End.X - 68f, row.Position.Y + 2f), new Vector2(56f, 24f));
			DrawButton(buyRect, _money >= entry.Price ? "买入" : "不足", _money >= entry.Price ? new Color(0.26f, 0.42f, 0.58f) : new Color(0.24f, 0.24f, 0.28f));
			if (_money >= entry.Price)
			{
				_buttons.Add(new ButtonDef(buyRect, "buy_shop", i));
			}
			shopY += 34f;
		}

		float soldierY = panel.End.Y - 110f;
		float soldierX = panel.Position.X + 22f;
		DrawString(ThemeDB.FallbackFont, new Vector2(soldierX, soldierY), "士兵名单", HorizontalAlignment.Left, -1f, 16, Colors.White);
		soldierY += 22f;
		if (_soldierRoster.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(soldierX, soldierY), "当前没有可用士兵。", HorizontalAlignment.Left, -1f, 13, new Color(0.72f, 0.76f, 0.82f));
		}
		else
		{
			for (int i = 0; i < _soldierRoster.Count && i < 6; i++)
			{
				DrawString(ThemeDB.FallbackFont, new Vector2(soldierX, soldierY), $"• {_soldierRoster[i].Name}", HorizontalAlignment.Left, -1f, 13, new Color(0.82f, 0.88f, 0.94f));
				soldierY += 18f;
			}
		}
	}

	private void DrawMap()
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

		foreach (MapNode node in _nodes)
		{
			if (!node.Revealed)
			{
				continue;
			}

			foreach (int link in node.Links)
			{
				if (!_nodes[link].Revealed)
				{
					continue;
				}

				if (link < node.Id) continue;
				bool highlight = HasClearVision(node) && HasClearVision(_nodes[link]);
				DrawMapPath(node.Position, _nodes[link].Position, highlight);
			}
		}

		foreach (MapNode node in _nodes)
		{
			if (!node.Revealed)
			{
				DrawCircle(node.Position, 10f, new Color(0.18f, 0.17f, 0.16f, 0.45f));
				DrawString(ThemeDB.FallbackFont, node.Position + new Vector2(-6f, -12f), "?", HorizontalAlignment.Left, -1f, 12, new Color(0.68f, 0.64f, 0.58f, 0.7f));
				continue;
			}

			bool clearVision = HasClearVision(node);
			Color color = GetNodeColor(node, clearVision);
			float outerAlpha = clearVision ? 0.95f : 0.55f;
			float labelAlpha = clearVision ? 1f : 0.65f;
			DrawCircle(node.Position, 24f, new Color(0.14f, 0.12f, 0.1f, outerAlpha));
			DrawCircle(node.Position, 20f, color);
			DrawArc(node.Position, 28f, 0f, Mathf.Tau, 32, new Color(color.R, color.G, color.B, clearVision ? 0.38f : 0.18f), 2f);
			DrawNodeGlyph(node, color);
			Vector2 labelPos = node.Position + GetNodeLabelOffset(node.Id);
			DrawString(ThemeDB.FallbackFont, labelPos, node.Name, HorizontalAlignment.Left, -1f, 14, new Color(0.96f, 0.93f, 0.86f, labelAlpha));
			if (node.Id == _playerNodeId && !_isPlayerMoving)
			{
				DrawArc(node.Position, 34f, 0f, Mathf.Tau, 32, new Color(0.58f, 0.95f, 0.98f, 0.9f), 3f);
				DrawCircle(node.Position, 7f, new Color(0.58f, 0.95f, 0.98f));
			}
			AiSquad squad = GetSquadAtNode(node.Id);
			if (squad != null && clearVision)
			{
				Vector2 badge = node.Position + new Vector2(20f, 18f);
				DrawCircle(badge, 9f, new Color(0.42f, 0.08f, 0.08f, 0.95f));
				DrawCircle(badge, 7f, new Color(0.92f, 0.34f, 0.3f));
				DrawString(ThemeDB.FallbackFont, node.Position + GetSquadLabelOffset(node.Id), squad.Name, HorizontalAlignment.Left, -1f, 10, new Color(1f, 0.84f, 0.8f));
				Vector2 intentPos = node.Position + GetSquadIntentOffset(node.Id);
				DrawAiIntentIcon(intentPos + new Vector2(6f, -3f), squad.Intent);
				DrawString(ThemeDB.FallbackFont, intentPos + new Vector2(18f, 0f), $"现：{GetAiIntentSummary(squad)}", HorizontalAlignment.Left, -1f, 9, new Color(0.92f, 0.92f, 0.84f));
				DrawString(ThemeDB.FallbackFont, intentPos + new Vector2(18f, 12f), $"后：{GetAiNextActionSummary(squad)}", HorizontalAlignment.Left, -1f, 9, new Color(0.72f, 0.86f, 0.98f, 0.95f));

				int nextNodeId = GetAiPredictedNextNodeId(squad);
				if (nextNodeId >= 0 && nextNodeId < _nodes.Count && nextNodeId != squad.NodeId)
				{
					DrawPredictedMoveArrow(node.Position, _nodes[nextNodeId].Position);
				}
			}
		}

		DrawArc(_playerMarkerPosition, 34f, 0f, Mathf.Tau, 32, new Color(0.58f, 0.95f, 0.98f, 0.9f), 3f);
		DrawCircle(_playerMarkerPosition, 7f, new Color(0.58f, 0.95f, 0.98f));
	}

	private void DrawRoomView()
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
		Rect2 banner = new(_mapRect.Position + new Vector2(18f, 18f), new Vector2(_mapRect.Size.X - 36f, 74f));
		DrawRect(banner, new Color(0.05f, 0.05f, 0.06f, 0.78f), true);
		DrawRect(banner, new Color(0.38f, 0.41f, 0.46f, 0.92f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, banner.Position + new Vector2(16f, 24f), $"战术房间: {node.Name}", HorizontalAlignment.Left, -1f, 18, Colors.White);
		DrawString(ThemeDB.FallbackFont, banner.Position + new Vector2(16f, 46f), $"类型 {GetNodeTypeLabel(node.Type)}  威胁 {node.Threat}  搜刮 {CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, 13, new Color(0.76f, 0.82f, 0.9f));
		DrawString(ThemeDB.FallbackFont, banner.Position + new Vector2(16f, 64f), "按 M 打开战略地图，默认操作留在房间层。", HorizontalAlignment.Left, -1f, 12, new Color(0.94f, 0.84f, 0.62f));

		Rect2 roomCore = new(_mapRect.Position + new Vector2(120f, 136f), _mapRect.Size - new Vector2(240f, 250f));
		DrawRect(roomCore, new Color(0.08f, 0.09f, 0.11f, 0.7f), true);
		DrawRect(roomCore, new Color(0.42f, 0.45f, 0.5f, 0.95f), false, 2f);
		DrawString(ThemeDB.FallbackFont, roomCore.Position + new Vector2(18f, 26f), "房间内部", HorizontalAlignment.Left, -1f, 18, Colors.White);
		DrawString(ThemeDB.FallbackFont, roomCore.Position + new Vector2(18f, 48f), "这里承接搜索、战斗和转场决策。", HorizontalAlignment.Left, -1f, 12, new Color(0.82f, 0.86f, 0.92f));
		DrawString(ThemeDB.FallbackFont, roomCore.Position + new Vector2(18f, 68f), _plannedExitNodeId >= 0 ? $"已规划出口：{_nodes[_plannedExitNodeId].Name}" : "尚未规划出口，可直接在房间内选出口。", HorizontalAlignment.Left, -1f, 12, new Color(0.94f, 0.84f, 0.62f));
		DrawRect(new Rect2(roomCore.Position + new Vector2(28f, 96f), new Vector2(roomCore.Size.X - 56f, roomCore.Size.Y - 132f)), new Color(0.16f, 0.18f, 0.2f, 0.45f), false, 2f);

		float exitY = _mapRect.End.Y - 110f;
		DrawString(ThemeDB.FallbackFont, new Vector2(_mapRect.Position.X + 24f, exitY), "房间出口", HorizontalAlignment.Left, -1f, 16, Colors.White);
		exitY += 24f;
		if (node.Links.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(_mapRect.Position.X + 24f, exitY), "当前房间没有可用出口。", HorizontalAlignment.Left, -1f, 13, new Color(0.76f, 0.8f, 0.86f));
			return;
		}

		for (int i = 0; i < node.Links.Count && i < 4; i++)
		{
			MapNode linkedNode = _nodes[node.Links[i]];
			string exitLabel = $"{GetExitDirectionLabel(i, node.Links.Count)} -> {linkedNode.Name}";
			DrawString(ThemeDB.FallbackFont, new Vector2(_mapRect.Position.X + 24f, exitY), exitLabel, HorizontalAlignment.Left, -1f, 13, new Color(0.76f, 0.8f, 0.86f));
			exitY += 18f;
		}

		DrawRoomExits(node);
	}

	private void DrawMapOverlay()
	{
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.34f), true);
		DrawMap();

		Rect2 titleBar = new(_mapRect.Position + new Vector2(18f, 18f), new Vector2(_mapRect.Size.X - 36f, 64f));
		DrawRect(titleBar, new Color(0.04f, 0.05f, 0.06f, 0.9f), true);
		DrawRect(titleBar, new Color(0.46f, 0.5f, 0.55f, 0.95f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, titleBar.Position + new Vector2(16f, 24f), "战略地图", HorizontalAlignment.Left, -1f, 18, Colors.White);
		DrawString(ThemeDB.FallbackFont, titleBar.Position + new Vector2(16f, 46f), "点击相邻节点执行转场，再按 M 返回房间视图。", HorizontalAlignment.Left, -1f, 12, new Color(0.86f, 0.9f, 0.95f));
	}

	private void DrawRoomExits(MapNode node)
	{
		for (int i = 0; i < node.Links.Count && i < 4; i++)
		{
			int linkedNodeId = node.Links[i];
			MapNode linkedNode = _nodes[linkedNodeId];
			Rect2 exitRect = GetRoomExitRect(i, node.Links.Count);
			bool planned = linkedNodeId == _plannedExitNodeId;
			Color fill = planned ? new Color(0.3f, 0.54f, 0.68f, 0.9f) : new Color(0.18f, 0.2f, 0.24f, 0.92f);
			Color border = planned ? new Color(0.92f, 0.84f, 0.58f, 0.98f) : new Color(0.62f, 0.68f, 0.76f, 0.95f);
			DrawRect(exitRect, fill, true);
			DrawRect(exitRect, border, false, 2f);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(10f, 18f), GetExitDirectionLabel(i, node.Links.Count), HorizontalAlignment.Left, exitRect.Size.X - 20f, 12, Colors.White);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(10f, 36f), linkedNode.Name, HorizontalAlignment.Left, exitRect.Size.X - 20f, 12, new Color(0.88f, 0.92f, 0.98f));
			_buttons.Add(new ButtonDef(exitRect, "use_exit", linkedNodeId));
		}
	}

	private Rect2 GetRoomExitRect(int index, int totalCount)
	{
		Vector2 size = new(112f, 46f);
		Vector2 center = _mapRect.GetCenter();
		Vector2 position = totalCount switch
		{
			1 => center + new Vector2(_mapRect.Size.X * 0.5f - size.X - 28f, -size.Y * 0.5f),
			2 => index == 0
				? new Vector2(_mapRect.Position.X + 22f, center.Y - size.Y * 0.5f)
				: new Vector2(_mapRect.End.X - size.X - 22f, center.Y - size.Y * 0.5f),
			3 => index switch
			{
				0 => new Vector2(_mapRect.Position.X + 22f, center.Y - size.Y * 0.5f),
				1 => new Vector2(center.X - size.X * 0.5f, _mapRect.Position.Y + 106f),
				_ => new Vector2(_mapRect.End.X - size.X - 22f, center.Y - size.Y * 0.5f),
			},
			_ => index switch
			{
				0 => new Vector2(_mapRect.Position.X + 22f, center.Y - size.Y * 0.5f),
				1 => new Vector2(center.X - size.X * 0.5f, _mapRect.Position.Y + 106f),
				2 => new Vector2(_mapRect.End.X - size.X - 22f, center.Y - size.Y * 0.5f),
				_ => new Vector2(center.X - size.X * 0.5f, _mapRect.End.Y - size.Y - 126f),
			},
		};
		return new Rect2(position, size);
	}

	private Rect2 GetRoomExitRect(RoomDoorSide side)
	{
		Vector2 size = new(112f, 46f);
		Vector2 center = _mapRect.GetCenter();
		Vector2 position = side switch
		{
			RoomDoorSide.Left => new Vector2(_mapRect.Position.X + 22f, center.Y - size.Y * 0.5f),
			RoomDoorSide.Top => new Vector2(center.X - size.X * 0.5f, _mapRect.Position.Y + 106f),
			RoomDoorSide.Right => new Vector2(_mapRect.End.X - size.X - 22f, center.Y - size.Y * 0.5f),
			_ => new Vector2(center.X - size.X * 0.5f, _mapRect.End.Y - size.Y - 126f),
		};
		return new Rect2(position, size);
	}

	private string GetExitDirectionLabel(RoomDoorSide side) => side switch
	{
		RoomDoorSide.Left => "瑗夸晶",
		RoomDoorSide.Top => "鍖椾晶",
		RoomDoorSide.Right => "涓滀晶",
		_ => "鍗椾晶",
	};

	private string GetExitDirectionLabel(int index, int totalCount) => totalCount switch
	{
		1 => index == 0 ? "主出口" : "出口",
		2 => index == 0 ? "西侧" : "东侧",
		3 => index switch
		{
			0 => "西侧",
			1 => "北侧",
			_ => "东侧",
		},
		_ => index switch
		{
			0 => "西侧",
			1 => "北侧",
			2 => "东侧",
			_ => "南侧",
		},
	};

	private string GetCleanExitDirectionLabel(RoomDoorSide side) => side switch
	{
		RoomDoorSide.Left => "西侧",
		RoomDoorSide.Top => "北侧",
		RoomDoorSide.Right => "东侧",
		_ => "南侧",
	};

	private void DrawMonasteryBackdrop()
	{
		DrawRect(new Rect2(_mapRect.Position + new Vector2(18f, 18f), _mapRect.Size - new Vector2(36f, 36f)), new Color(0.17f, 0.15f, 0.12f), true);
		DrawRect(new Rect2(_mapRect.Position + new Vector2(28f, 28f), _mapRect.Size - new Vector2(56f, 56f)), new Color(0.22f, 0.2f, 0.16f), false, 2f);

		Rect2 gate = new(new Vector2(45f, 285f), new Vector2(125f, 145f));
		Rect2 cemetery = new(new Vector2(70f, 150f), new Vector2(110f, 120f));
		Rect2 stable = new(new Vector2(85f, 455f), new Vector2(92f, 115f));
		Rect2 archive = new(new Vector2(175f, 125f), new Vector2(115f, 102f));
		Rect2 barracks = new(new Vector2(200f, 455f), new Vector2(125f, 115f));
		Rect2 cloister = new(new Vector2(255f, 145f), new Vector2(280f, 305f));
		Rect2 courtyard = new(new Vector2(320f, 215f), new Vector2(145f, 150f));
		Rect2 chapel = new(new Vector2(535f, 145f), new Vector2(125f, 105f));
		Rect2 reliquary = new(new Vector2(640f, 235f), new Vector2(82f, 112f));
		Rect2 bellTower = new(new Vector2(552f, 328f), new Vector2(96f, 112f));
		Rect2 crypt = new(new Vector2(535f, 455f), new Vector2(170f, 120f));

		DrawDistrictBlock(gate, new Color(0.19f, 0.17f, 0.14f), "前庭", new Vector2(12f, 22f));
		DrawDistrictBlock(cemetery, new Color(0.18f, 0.17f, 0.15f), "墓园", new Vector2(12f, 22f));
		DrawDistrictBlock(stable, new Color(0.21f, 0.17f, 0.14f), "马厩", new Vector2(12f, 22f));
		DrawDistrictBlock(archive, new Color(0.21f, 0.18f, 0.16f), "抄经室", new Vector2(12f, 20f));
		DrawDistrictBlock(barracks, new Color(0.2f, 0.17f, 0.14f), "宿舍", new Vector2(12f, 22f));
		DrawDistrictBlock(cloister, new Color(0.2f, 0.19f, 0.15f), "回廊", new Vector2(18f, 24f));
		DrawDistrictBlock(courtyard, new Color(0.14f, 0.18f, 0.13f), "中庭", new Vector2(48f, 80f));
		DrawDistrictBlock(chapel, new Color(0.23f, 0.2f, 0.17f), "礼拜堂", new Vector2(18f, 22f));
		DrawDistrictBlock(reliquary, new Color(0.25f, 0.21f, 0.16f), "圣物库", new Vector2(8f, 22f));
		DrawDistrictBlock(bellTower, new Color(0.21f, 0.18f, 0.17f), "钟楼", new Vector2(12f, 22f));
		DrawDistrictBlock(crypt, new Color(0.16f, 0.15f, 0.18f), "地窖", new Vector2(12f, 22f));

		DrawLine(new Vector2(182f, 360f), new Vector2(250f, 360f), new Color(0.46f, 0.4f, 0.31f, 0.8f), 12f);
		DrawLine(new Vector2(520f, 360f), new Vector2(620f, 360f), new Color(0.46f, 0.4f, 0.31f, 0.8f), 12f);
		DrawLine(new Vector2(620f, 255f), new Vector2(620f, 455f), new Color(0.46f, 0.4f, 0.31f, 0.8f), 12f);
		DrawLine(new Vector2(280f, 505f), new Vector2(550f, 505f), new Color(0.46f, 0.4f, 0.31f, 0.8f), 10f);
	}

	private void DrawBorderKeepBackdrop()
	{
		DrawRect(new Rect2(_mapRect.Position + new Vector2(18f, 18f), _mapRect.Size - new Vector2(36f, 36f)), new Color(0.16f, 0.15f, 0.13f), true);
		DrawRect(new Rect2(_mapRect.Position + new Vector2(34f, 34f), _mapRect.Size - new Vector2(68f, 68f)), new Color(0.22f, 0.2f, 0.18f), false, 2f);

		Rect2 outerYard = new(new Vector2(70f, 430f), new Vector2(230f, 160f));
		Rect2 ditch = new(new Vector2(130f, 330f), new Vector2(165f, 80f));
		Rect2 westWall = new(new Vector2(205f, 120f), new Vector2(180f, 120f));
		Rect2 mainYard = new(new Vector2(280f, 240f), new Vector2(350f, 290f));
		Rect2 armory = new(new Vector2(470f, 120f), new Vector2(120f, 105f));
		Rect2 command = new(new Vector2(515f, 255f), new Vector2(170f, 125f));
		Rect2 eastTower = new(new Vector2(650f, 135f), new Vector2(78f, 125f));
		Rect2 supplyYard = new(new Vector2(575f, 405f), new Vector2(130f, 110f));
		Rect2 dungeon = new(new Vector2(435f, 515f), new Vector2(150f, 75f));

		DrawDistrictBlock(outerYard, new Color(0.2f, 0.17f, 0.14f), "", Vector2.Zero);
		DrawDistrictBlock(ditch, new Color(0.15f, 0.16f, 0.17f), "", Vector2.Zero);
		DrawDistrictBlock(westWall, new Color(0.19f, 0.18f, 0.17f), "", Vector2.Zero);
		DrawDistrictBlock(mainYard, new Color(0.18f, 0.18f, 0.15f), "", Vector2.Zero);
		DrawDistrictBlock(armory, new Color(0.21f, 0.18f, 0.15f), "", Vector2.Zero);
		DrawDistrictBlock(command, new Color(0.23f, 0.19f, 0.16f), "", Vector2.Zero);
		DrawDistrictBlock(eastTower, new Color(0.19f, 0.17f, 0.17f), "", Vector2.Zero);
		DrawDistrictBlock(supplyYard, new Color(0.2f, 0.18f, 0.14f), "", Vector2.Zero);
		DrawDistrictBlock(dungeon, new Color(0.15f, 0.14f, 0.17f), "", Vector2.Zero);

		DrawLine(new Vector2(235f, 510f), new Vector2(650f, 510f), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f);
		DrawLine(new Vector2(390f, 170f), new Vector2(650f, 170f), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f);
		DrawLine(new Vector2(390f, 170f), new Vector2(390f, 510f), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f);
		DrawLine(new Vector2(650f, 170f), new Vector2(650f, 510f), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f);
	}

	private void DrawDistrictBlock(Rect2 rect, Color fill, string label, Vector2 labelOffset)
	{
		DrawRect(rect, fill, true);
		DrawRect(rect, new Color(0.52f, 0.46f, 0.36f, 0.95f), false, 2f);
	}

	private void DrawMapPath(Vector2 from, Vector2 to, bool highlight)
	{
		Color baseColor = highlight
			? new Color(0.72f, 0.64f, 0.47f, 0.82f)
			: new Color(0.48f, 0.42f, 0.32f, 0.26f);
		float width = highlight ? 3.2f : 1.6f;
		DrawLine(from, to, baseColor, width);
	}

	private void DrawNodeGlyph(MapNode node, Color color)
	{
		Vector2 p = node.Position;
		Color ink = new Color(0.09f, 0.08f, 0.07f, 0.9f);
		switch (node.Type)
		{
			case NodeType.Extract:
				DrawLine(p + new Vector2(-7f, 0f), p + new Vector2(8f, 0f), ink, 2f);
				DrawLine(p + new Vector2(3f, -5f), p + new Vector2(8f, 0f), ink, 2f);
				DrawLine(p + new Vector2(3f, 5f), p + new Vector2(8f, 0f), ink, 2f);
				break;
			case NodeType.Battle:
				DrawLine(p + new Vector2(-7f, -7f), p + new Vector2(7f, 7f), ink, 2.2f);
				DrawLine(p + new Vector2(-7f, 7f), p + new Vector2(7f, -7f), ink, 2.2f);
				break;
			case NodeType.Search:
				DrawCircle(p + new Vector2(-1f, -1f), 6f, ink);
				DrawLine(p + new Vector2(4f, 4f), p + new Vector2(10f, 10f), ink, 2f);
				DrawCircle(p + new Vector2(-1f, -1f), 3.5f, color);
				break;
			default:
				DrawRect(new Rect2(p + new Vector2(-6f, -6f), new Vector2(12f, 12f)), ink, false, 2f);
				break;
		}
	}

	private Vector2 GetNodeLabelOffset(int nodeId) => nodeId switch
	{
		0 => new Vector2(-22f, -62f),
		1 => new Vector2(-48f, -50f),
		2 => new Vector2(-44f, -52f),
		3 => new Vector2(-42f, -48f),
		4 => new Vector2(-48f, -46f),
		5 => new Vector2(-18f, -50f),
		6 => new Vector2(-18f, -50f),
		7 => new Vector2(-14f, -48f),
		8 => new Vector2(-46f, -48f),
		9 => new Vector2(-18f, -48f),
		10 => new Vector2(-20f, -50f),
		11 => new Vector2(-16f, -48f),
		12 => new Vector2(-14f, -48f),
		13 => new Vector2(-44f, -42f),
		14 => new Vector2(-48f, -42f),
		_ => new Vector2(-42f, -34f),
	};

	private Vector2 GetSquadLabelOffset(int nodeId) => nodeId switch
	{
		3 => new Vector2(-6f, 30f),
		9 => new Vector2(10f, 30f),
		10 => new Vector2(10f, 30f),
		12 => new Vector2(6f, 30f),
		_ => new Vector2(14f, 42f),
	};

	private Vector2 GetSquadIntentOffset(int nodeId) => nodeId switch
	{
		3 => new Vector2(-6f, 42f),
		9 => new Vector2(10f, 42f),
		10 => new Vector2(10f, 42f),
		12 => new Vector2(6f, 42f),
		_ => new Vector2(14f, 54f),
	};

	private void DrawAiIntentIcon(Vector2 center, AiIntent intent)
	{
		Color fg = new(0.92f, 0.92f, 0.84f, 0.95f);
		Color bg = new(0.08f, 0.08f, 0.09f, 0.72f);
		DrawCircle(center, 7f, bg);

		switch (intent)
		{
			case AiIntent.Moving:
			case AiIntent.Extracting:
				DrawLine(center + new Vector2(-3f, 0f), center + new Vector2(2f, 0f), fg, 1.6f);
				DrawLine(center + new Vector2(2f, 0f), center + new Vector2(-1f, -3f), fg, 1.6f);
				DrawLine(center + new Vector2(2f, 0f), center + new Vector2(-1f, 3f), fg, 1.6f);
				break;
			case AiIntent.Clearing:
				DrawLine(center + new Vector2(-3f, -3f), center + new Vector2(3f, 3f), fg, 1.6f);
				DrawLine(center + new Vector2(-3f, 3f), center + new Vector2(3f, -3f), fg, 1.6f);
				break;
			case AiIntent.Looting:
				DrawRect(new Rect2(center + new Vector2(-3.5f, -2.5f), new Vector2(7f, 5f)), fg, false, 1.4f);
				DrawLine(center + new Vector2(-1.5f, -4f), center + new Vector2(1.5f, -4f), fg, 1.4f);
				DrawLine(center + new Vector2(-1.5f, -4f), center + new Vector2(-1.5f, -2.5f), fg, 1.4f);
				DrawLine(center + new Vector2(1.5f, -4f), center + new Vector2(1.5f, -2.5f), fg, 1.4f);
				break;
			case AiIntent.Fighting:
				DrawLine(center + new Vector2(-4f, 0f), center + new Vector2(0f, -3f), fg, 1.6f);
				DrawLine(center + new Vector2(0f, -3f), center + new Vector2(4f, 0f), fg, 1.6f);
				DrawLine(center + new Vector2(-4f, 0f), center + new Vector2(0f, 3f), fg, 1.6f);
				DrawLine(center + new Vector2(0f, 3f), center + new Vector2(4f, 0f), fg, 1.6f);
				break;
			case AiIntent.Extracted:
				DrawArc(center, 4f, 0f, Mathf.Tau, 16, fg, 1.5f);
				DrawLine(center + new Vector2(0f, -2f), center + new Vector2(0f, 2f), fg, 1.3f);
				break;
			case AiIntent.Defeated:
				DrawLine(center + new Vector2(-3f, -3f), center + new Vector2(3f, 3f), fg, 1.6f);
				DrawLine(center + new Vector2(-3f, 3f), center + new Vector2(3f, -3f), fg, 1.6f);
				DrawCircle(center, 1.5f, fg);
				break;
			default:
				DrawCircle(center, 2f, fg);
				break;
		}
	}

	private void DrawPredictedMoveArrow(Vector2 from, Vector2 to)
	{
		Vector2 direction = (to - from).Normalized();
		if (direction == Vector2.Zero)
		{
			return;
		}

		Vector2 start = from + direction * 30f;
		Vector2 end = from + direction * 54f;
		Color color = new(0.62f, 0.88f, 0.96f, 0.8f);
		DrawLine(start, end, color, 1.8f);

		Vector2 normal = new(-direction.Y, direction.X);
		DrawLine(end, end - direction * 7f + normal * 4f, color, 1.8f);
		DrawLine(end, end - direction * 7f - normal * 4f, color, 1.8f);
	}

	private Color GetNodeColor(MapNode node, bool clearVision)
	{
		if (!clearVision)
		{
			return node.Visited ? new Color(0.28f, 0.34f, 0.4f, 0.85f) : new Color(0.2f, 0.22f, 0.25f, 0.75f);
		}

		if (node.Type == NodeType.Extract) return new Color(0.26f, 0.72f, 0.42f);
		if (node.Threat > 0) return new Color(0.72f, 0.3f, 0.28f);
		if (CountNodeLoot(node) > 0) return new Color(0.75f, 0.62f, 0.24f);
		return node.Visited ? new Color(0.31f, 0.45f, 0.62f) : new Color(0.2f, 0.23f, 0.28f);
	}

	private void DrawSidePanel()
	{
		DrawRect(_sideRect, new Color(0.05f, 0.05f, 0.06f, 0.96f), true);
		DrawRect(_sideRect, Colors.White, false, 2f);
		float x = _sideRect.Position.X + 18f;
		float y = _sideRect.Position.Y + 28f;
		float panelBottom = _sideRect.End.Y - 18f;
		MapNode node = _nodes[_playerNodeId];
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "\u8282\u70b9\u7a81\u88ad Demo", HorizontalAlignment.Left, -1f, 20, Colors.White);
		y += 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u56de\u5408 {_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.76f, 0.84f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u751f\u547d {_playerHp}/{_playerMaxHp}   \u6218\u529b {_playerStrength}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 22f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u65f6\u9699 {_timeSlotProgress}/100   \u6218\u5229\u54c1\u4ef7\u503c {_lootValue}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 20f;
		Rect2 timeBar = new(new Vector2(x, y), new Vector2(312f, 10f));
		DrawRect(timeBar, new Color(0.12f, 0.13f, 0.16f), true);
		DrawRect(timeBar, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawRect(new Rect2(timeBar.Position, new Vector2(timeBar.Size.X * (_timeSlotProgress / 100f), timeBar.Size.Y)), new Color(0.84f, 0.66f, 0.26f), true);
		y += 32f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, 320f, 14, new Color(0.86f, 0.9f, 0.95f));
		y += 54f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u5f53\u524d\u8282\u70b9\uff1a{node.Name}", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u7c7b\u578b\uff1a{GetNodeTypeLabel(node.Type)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 18f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u5a01\u80c1\uff1a{node.Threat}   \u6218\u5229\u54c1\uff1a{CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 28f;
		Rect2 mapRect = new(new Vector2(x, y), new Vector2(148f, 28f));
		DrawButton(mapRect, _showMapOverlay ? "战略地图：开" : "战略地图：关", _showMapOverlay ? new Color(0.24f, 0.48f, 0.62f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(mapRect, "toggle_map"));
		Rect2 autoRect = new(new Vector2(x, y), new Vector2(148f, 28f));
		autoRect.Position += new Vector2(166f, 0f);
		DrawButton(autoRect, _autoSearchEnabled ? "\u81ea\u52a8\u641c\u7d22\uff1a\u5f00" : "\u81ea\u52a8\u641c\u7d22\uff1a\u5173", _autoSearchEnabled ? new Color(0.24f, 0.56f, 0.32f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(autoRect, "toggle_auto_search"));
		if (node.Type == NodeType.Extract && !_runEnded)
		{
			Rect2 rect = new(new Vector2(x + 166f, y + 34f), new Vector2(148f, 28f));
			DrawButton(rect, "\u6267\u884c\u64a4\u79bb", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(rect, "extract"));
		}
		y += node.Type == NodeType.Extract ? 74f : 42f;
		if (CanSearch(node) && CountNodeLoot(node) > 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "\u5bb9\u5668", HorizontalAlignment.Left, -1f, 16, Colors.White);
			y += 22f;
			for (int i = 0; i < node.Containers.Count && i < 6; i++)
			{
				LootContainer container = node.Containers[i];
				if (container.IsEmpty) continue;
				Rect2 rowRect = new(new Vector2(x, y), new Vector2(320f, 24f));
				Color fill = i == _selectedContainerIndex ? new Color(0.26f, 0.3f, 0.38f) : new Color(0.12f, 0.13f, 0.16f);
				DrawRect(rowRect, fill, true);
				DrawRect(rowRect, new Color(0.34f, 0.37f, 0.42f), false, 1f);
				DrawString(ThemeDB.FallbackFont, rowRect.Position + new Vector2(10f, 17f), container.Label, HorizontalAlignment.Left, 170f, 12, Colors.White);
				DrawString(ThemeDB.FallbackFont, rowRect.Position + new Vector2(182f, 17f), $"\u660e\u9762 {CountRevealedGridItems(container) + CountAvailableEquipped(container)}", HorizontalAlignment.Left, 60f, 11, new Color(0.78f, 0.87f, 0.98f));
				DrawString(ThemeDB.FallbackFont, rowRect.Position + new Vector2(238f, 17f), $"\u672a\u63ed {CountHiddenGridItems(container)}", HorizontalAlignment.Left, 56f, 11, new Color(0.92f, 0.84f, 0.6f));
				Rect2 openRect = new(new Vector2(rowRect.End.X - 68f, rowRect.Position.Y + 2f), new Vector2(56f, 20f));
				DrawButton(openRect, "\u6253\u5f00", new Color(0.23f, 0.4f, 0.58f));
				_buttons.Add(new ButtonDef(openRect, "open_container", i));
				y += 30f;
			}
		}
		float logTop = panelBottom - 120f;
		DrawLine(new Vector2(x, logTop - 10f), new Vector2(_sideRect.End.X - 18f, logTop - 10f), new Color(0.24f, 0.27f, 0.31f), 1f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logTop), "\u4e16\u754c\u52a8\u6001", HorizontalAlignment.Left, -1f, 16, Colors.White);
		float logY = logTop + 20f;
		int startIndex = Mathf.Max(0, _eventLog.Count - 5);
		for (int i = startIndex; i < _eventLog.Count; i++)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), _eventLog[i], HorizontalAlignment.Left, 320f, 12, new Color(0.8f, 0.84f, 0.9f));
			logY += 18f;
		}
	}

	private void DrawButton(Rect2 rect, string text, Color color)
	{
		DrawRect(rect, color, true);
		DrawRect(rect, Colors.White, false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(8f, 17f), text, HorizontalAlignment.Left, rect.Size.X - 12f, 12, Colors.White);
	}

	private float GetContainerCardHeight(LootContainer container)
	{
		int visibleRows = container.VisibleItems.Count;
		int hiddenRows = container.HiddenRemaining > 0 ? 1 : 0;
		int equipmentRows = container.Kind == ContainerKind.EliteCorpse ? 3 : 0;
		return 48f + equipmentRows * 34f + visibleRows * 34f + hiddenRows * 38f;
	}

	private void DrawContainerCard(LootContainer container, int containerIndex, float x, ref float y)
	{
		float width = 320f;
		float height = GetContainerCardHeight(container);
		Rect2 card = new(new Vector2(x, y), new Vector2(width, height));
		DrawRect(card, new Color(0.08f, 0.09f, 0.11f), true);
		DrawRect(card, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);

		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(12f, 20f), container.Label, HorizontalAlignment.Left, 200f, 14, Colors.White);
		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(210f, 20f), GetContainerKindLabel(container.Kind), HorizontalAlignment.Left, 100f, 12, new Color(0.93f, 0.84f, 0.58f));

		float rowY = card.Position.Y + 30f;
		if (container.Kind == ContainerKind.EliteCorpse)
		{
			for (int equipIndex = 0; equipIndex < container.EquippedItems.Count; equipIndex++)
			{
				EquippedLoot equipped = container.EquippedItems[equipIndex];
				Rect2 slot = new(new Vector2(x + 12f, rowY), new Vector2(304f, 28f));
				DrawRect(slot, new Color(0.14f, 0.16f, 0.2f), true);
				DrawRect(slot, new Color(0.5f, 0.66f, 0.86f), false, 1f);

				string label = string.IsNullOrEmpty(equipped.Label) || equipped.Taken
					? $"{GetEquipmentSlotLabel(equipped.Slot)}：空"
					: $"{GetEquipmentSlotLabel(equipped.Slot)}：{equipped.Label}";
				DrawString(ThemeDB.FallbackFont, slot.Position + new Vector2(10f, 19f), label, HorizontalAlignment.Left, 180f, 12, Colors.White);

				if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
				{
					Rect2 takeRect = new(new Vector2(slot.End.X - 70f, slot.Position.Y + 2f), new Vector2(58f, 24f));
					DrawButton(takeRect, "拿取", new Color(0.26f, 0.42f, 0.58f));
					_buttons.Add(new ButtonDef(takeRect, "take", containerIndex * 100 + 50 + equipIndex));
				}

				rowY += 34f;
			}
		}

		for (int itemIndex = 0; itemIndex < container.VisibleItems.Count; itemIndex++)
		{
			string item = container.VisibleItems[itemIndex];
			Rect2 slot = new(new Vector2(x + 12f, rowY), new Vector2(304f, 28f));
			DrawRect(slot, new Color(0.12f, 0.17f, 0.23f), true);
			DrawRect(slot, new Color(0.5f, 0.66f, 0.86f), false, 1f);
			DrawString(ThemeDB.FallbackFont, slot.Position + new Vector2(10f, 19f), item, HorizontalAlignment.Left, 160f, 12, Colors.White);

			Rect2 takeRect = new(new Vector2(slot.End.X - 70f, slot.Position.Y + 2f), new Vector2(58f, 24f));
			DrawButton(takeRect, "拿取", new Color(0.26f, 0.42f, 0.58f));
			_buttons.Add(new ButtonDef(takeRect, "take", containerIndex * 100 + itemIndex));
			rowY += 34f;
		}

		if (container.HiddenRemaining > 0)
		{
			Rect2 slot = new(new Vector2(x + 12f, rowY), new Vector2(304f, 32f));
			DrawRect(slot, new Color(0.24f, 0.2f, 0.12f), true);
			DrawRect(slot, new Color(0.76f, 0.63f, 0.28f), false, 1f);
			DrawString(ThemeDB.FallbackFont, slot.Position + new Vector2(10f, 21f), $"未明物品 x{container.HiddenRemaining}", HorizontalAlignment.Left, 150f, 12, Colors.White);

			Rect2 searchRect = new(new Vector2(slot.End.X - 94f, slot.Position.Y + 4f), new Vector2(82f, 24f));
			DrawButton(searchRect, "检视", new Color(0.54f, 0.42f, 0.18f));
			_buttons.Add(new ButtonDef(searchRect, "search", containerIndex));
		}

		y += height;
	}

	private int CountAvailableEquipped(LootContainer container)
	{
		int count = 0;
		foreach (EquippedLoot equipped in container.EquippedItems)
		{
			if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
			{
				count++;
			}
		}
		return count;
	}

	private int CountRevealedGridItems(LootContainer container)
	{
		if (container.GridItems.Count == 0)
		{
			return container.VisibleItems.Count;
		}

		int count = 0;
		foreach (GridLootItem item in container.GridItems)
		{
			if (item.Revealed && !item.Taken)
			{
				count++;
			}
		}
		return count;
	}

	private int CountHiddenGridItems(LootContainer container)
	{
		if (container.GridItems.Count == 0)
		{
			return container.HiddenRemaining;
		}

		int count = 0;
		foreach (GridLootItem item in container.GridItems)
		{
			if (!item.Revealed && !item.Taken)
			{
				count++;
			}
		}
		return count;
	}
	private void DrawContainerPopup()
	{
		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex < 0 || _selectedContainerIndex >= node.Containers.Count)
		{
			return;
		}
		LootContainer container = node.Containers[_selectedContainerIndex];
		EnsureContainerGrid(container);
		Vector2 cellSize = new(26f, 26f);
		float equipmentHeight = container.Kind == ContainerKind.EliteCorpse ? 118f : 0f;
		float gridWidth = container.GridSize.X * cellSize.X + 26f;
		Vector2 panelSize = new(Mathf.Max(gridWidth + 28f, 356f), container.GridSize.Y * cellSize.Y + 96f + equipmentHeight);
		Rect2 panel = new((GetViewportRect().Size - panelSize) / 2f, panelSize);
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.38f), true);
		DrawRect(panel, new Color(0.08f, 0.09f, 0.11f), true);
		DrawRect(panel, Colors.White, false, 2f);
		Rect2 titleBar = new(panel.Position + new Vector2(1f, 1f), new Vector2(panel.Size.X - 2f, 30f));
		DrawRect(titleBar, new Color(0.12f, 0.13f, 0.16f), true);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14f, 21f), container.Label, HorizontalAlignment.Left, 190f, 14, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(panel.Size.X - 102f, 21f), GetContainerKindLabel(container.Kind), HorizontalAlignment.Left, 68f, 12, new Color(0.93f, 0.84f, 0.58f));
		Rect2 closeRect = new(new Vector2(panel.End.X - 28f, panel.Position.Y + 5f), new Vector2(20f, 20f));
		DrawRect(closeRect, new Color(0.26f, 0.14f, 0.14f), true);
		DrawRect(closeRect, Colors.White, false, 1.2f);
		DrawString(ThemeDB.FallbackFont, closeRect.Position + new Vector2(6f, 15f), "X", HorizontalAlignment.Left, -1f, 12, Colors.White);
		_buttons.Add(new ButtonDef(closeRect, "close_container"));
		float rowY = panel.Position.Y + 42f;
		if (container.Kind == ContainerKind.EliteCorpse)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(panel.Position.X + 14f, rowY), "装备栏", HorizontalAlignment.Left, -1f, 13, new Color(0.9f, 0.92f, 0.96f));
			rowY += 16f;
			for (int equipIndex = 0; equipIndex < container.EquippedItems.Count; equipIndex++)
			{
				EquippedLoot equipped = container.EquippedItems[equipIndex];
				Rect2 slot = new(new Vector2(panel.Position.X + 14f, rowY), new Vector2(panel.Size.X - 28f, 28f));
				DrawRect(slot, new Color(0.14f, 0.16f, 0.2f), true);
				DrawRect(slot, new Color(0.5f, 0.66f, 0.86f), false, 1f);
				string label = string.IsNullOrEmpty(equipped.Label) || equipped.Taken ? $"{GetEquipmentSlotLabel(equipped.Slot)}\uff1a\u7a7a" : $"{GetEquipmentSlotLabel(equipped.Slot)}\uff1a{equipped.Label}";
				DrawString(ThemeDB.FallbackFont, slot.Position + new Vector2(10f, 19f), label, HorizontalAlignment.Left, slot.Size.X - 86f, 12, Colors.White);
				if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
				{
					Rect2 takeRect = new(new Vector2(slot.End.X - 70f, slot.Position.Y + 2f), new Vector2(58f, 24f));
					DrawButton(takeRect, "\u62ff\u53d6", new Color(0.26f, 0.42f, 0.58f));
					_buttons.Add(new ButtonDef(takeRect, "take", _selectedContainerIndex * 100 + 50 + equipIndex));
				}
				rowY += 34f;
			}
			rowY += 8f;
		}

		DrawString(ThemeDB.FallbackFont, new Vector2(panel.Position.X + 14f, rowY), "背包", HorizontalAlignment.Left, -1f, 13, new Color(0.9f, 0.92f, 0.96f));
		rowY += 16f;
		Vector2 gridOrigin = new(panel.Position.X + (panel.Size.X - gridWidth) * 0.5f, rowY);
		for (int y = 0; y < container.GridSize.Y; y++)
		{
			for (int x = 0; x < container.GridSize.X; x++)
			{
				Rect2 cellRect = new(gridOrigin + new Vector2(x * cellSize.X, y * cellSize.Y), cellSize - new Vector2(2f, 2f));
				DrawRect(cellRect, new Color(0.09f, 0.1f, 0.12f), true);
				DrawRect(cellRect, new Color(0.24f, 0.26f, 0.3f), false, 1f);
			}
		}

		for (int itemIndex = 0; itemIndex < container.GridItems.Count; itemIndex++)
		{
			GridLootItem item = container.GridItems[itemIndex];
			if (item.Taken)
			{
				continue;
			}

			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - 2f, item.Size.Y * cellSize.Y - 2f));

			if (!item.Revealed)
			{
				DrawRect(itemRect, new Color(0.3f, 0.32f, 0.35f), true);
				DrawRect(itemRect, new Color(0.62f, 0.64f, 0.67f), false, 1.2f);
				DrawHiddenItemPattern(itemRect, new Color(0.55f, 0.57f, 0.6f, 0.65f));
				if (container.ActiveSearchItemIndex < 0 || container.ActiveSearchItemIndex == itemIndex)
				{
					_buttons.Add(new ButtonDef(itemRect, "search", _selectedContainerIndex * 100 + itemIndex));
				}
			}
			else
			{
				DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
				DrawRect(itemRect, Colors.White, false, 1.2f);
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(4f, 16f), item.Label, HorizontalAlignment.Left, itemRect.Size.X - 6f, 10, Colors.Black);
				_buttons.Add(new ButtonDef(itemRect, "take", _selectedContainerIndex * 100 + itemIndex));
			}

			if (container.ActiveSearchItemIndex == itemIndex && !item.Revealed)
			{
				Vector2 center = itemRect.GetCenter();
				float ratio = Mathf.Clamp(item.SearchProgress / item.SearchTime, 0f, 0.98f);
				float endAngle = -Mathf.Pi / 2f + Mathf.Tau * ratio;
				DrawArc(center, 10f, -Mathf.Pi / 2f, endAngle, 24, GetGridRarityColor(item.Rarity), 3f);
			}
		}
	}

	private void DrawHiddenItemPattern(Rect2 rect, Color color)
	{
		float left = rect.Position.X;
		float right = rect.End.X;
		float top = rect.Position.Y;
		float bottom = rect.End.Y;
		for (float offset = -rect.Size.Y; offset < rect.Size.X; offset += 8f)
		{
			float c = left + offset + bottom;
			List<Vector2> intersections = new(4);

			float xOnBottom = c - bottom;
			if (xOnBottom >= left && xOnBottom <= right) intersections.Add(new Vector2(xOnBottom, bottom));
			float xOnTop = c - top;
			if (xOnTop >= left && xOnTop <= right) intersections.Add(new Vector2(xOnTop, top));
			float yOnLeft = c - left;
			if (yOnLeft >= top && yOnLeft <= bottom) AddUniquePoint(intersections, new Vector2(left, yOnLeft));
			float yOnRight = c - right;
			if (yOnRight >= top && yOnRight <= bottom) AddUniquePoint(intersections, new Vector2(right, yOnRight));

			if (intersections.Count >= 2)
			{
				DrawLine(intersections[0], intersections[1], color, 1f);
			}
		}
	}

	private static void AddUniquePoint(List<Vector2> points, Vector2 point)
	{
		foreach (Vector2 existing in points)
		{
			if (existing.DistanceSquaredTo(point) < 0.25f)
			{
				return;
			}
		}

		points.Add(point);
	}

	private static Color GetGridRarityColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => new Color(0.82f, 0.84f, 0.87f),
		ItemRarity.Green => new Color(0.48f, 0.85f, 0.45f),
		ItemRarity.Blue => new Color(0.36f, 0.7f, 1f),
		ItemRarity.Purple => new Color(0.72f, 0.48f, 0.92f),
		_ => new Color(1f, 0.82f, 0.32f),
	};

	private void DrawEncounterPrompt()
	{
		if (_pendingEncounter == null)
		{
			return;
		}

		Rect2 panel = new(new Vector2(360f, 250f), new Vector2(420f, 170f));
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.45f), true);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.98f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 30f), "遭遇敌人", HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 66f), _pendingEncounter.PromptText, HorizontalAlignment.Left, 380f, 14, new Color(0.84f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 96f), $"敌方：{_pendingEncounter.EnemyName}", HorizontalAlignment.Left, 380f, 13, new Color(0.95f, 0.78f, 0.78f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 118f), "当前仅可选择战斗。", HorizontalAlignment.Left, 380f, 13, new Color(0.86f, 0.9f, 0.95f));
		Rect2 fightRect = new(new Vector2(panel.Position.X + 20f, panel.End.Y - 42f), new Vector2(92f, 28f));
		DrawButton(fightRect, "战斗", new Color(0.54f, 0.26f, 0.22f));
		_buttons.Add(new ButtonDef(fightRect, "encounter_fight"));
	}

	private void DrawSearchConfirmDialog()
	{
		Rect2 panel = new(new Vector2(360f, 240f), new Vector2(480f, 210f));
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.45f), true);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.98f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 32f), "\u5151\u6362\u641c\u7d22\u6b21\u6570", HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 68f), "\u5f53\u524d\u641c\u7d22\u6b21\u6570\u4e0d\u8db3\u3002\u662f\u5426\u82b1\u8d39 1 \u56de\u5408\u6362\u53d6 4 \u6b21\u641c\u7d22\uff0c", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 90f), "\u5e76\u7acb\u5373\u63ed\u793a\u8fd9\u4e2a\u5bb9\u5668\u7684\u4e0b\u4e00\u4ef6\u7269\u54c1\uff1f", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		Rect2 checkRect = new(new Vector2(panel.Position.X + 24f, panel.Position.Y + 126f), new Vector2(18f, 18f));
		DrawRect(checkRect, new Color(0.08f, 0.08f, 0.1f), true);
		DrawRect(checkRect, Colors.White, false, 1.2f);
		if (_confirmSkipChecked)
		{
			DrawLine(checkRect.Position + new Vector2(3f, 9f), checkRect.Position + new Vector2(7f, 14f), new Color(0.56f, 0.95f, 0.62f), 2f);
			DrawLine(checkRect.Position + new Vector2(7f, 14f), checkRect.Position + new Vector2(15f, 3f), new Color(0.56f, 0.95f, 0.62f), 2f);
		}
		_buttons.Add(new ButtonDef(checkRect, "toggle_confirm_skip"));
		DrawString(ThemeDB.FallbackFont, checkRect.Position + new Vector2(28f, 14f), "\u672c\u6b21\u63a2\u7d22\u540e\u7eed\u4e0d\u518d\u63d0\u793a", HorizontalAlignment.Left, -1f, 13, Colors.White);
		Rect2 yesRect = new(new Vector2(panel.Position.X + 24f, panel.End.Y - 46f), new Vector2(90f, 28f));
		Rect2 noRect = new(new Vector2(panel.Position.X + 126f, panel.End.Y - 46f), new Vector2(90f, 28f));
		DrawButton(yesRect, "\u786e\u8ba4", new Color(0.24f, 0.56f, 0.34f));
		DrawButton(noRect, "\u53d6\u6d88", new Color(0.38f, 0.22f, 0.22f));
		_buttons.Add(new ButtonDef(yesRect, "confirm_search_yes"));
		_buttons.Add(new ButtonDef(noRect, "confirm_search_no"));
	}
	private void DrawEndOverlay()
	{
		Vector2 viewport = GetViewportRect().Size;
		Rect2 panel = new(viewport * 0.5f - new Vector2(360f, 210f), new Vector2(720f, 420f));
		DrawRect(panel, new Color(0.01f, 0.01f, 0.02f, 0.96f), true);
		DrawRect(panel, Colors.White, false, 2f);

		float x = panel.Position.X + 26f;
		float y = panel.Position.Y + 38f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _runFailed ? "行动失败" : "撤离完成", HorizontalAlignment.Left, -1f, 24, Colors.White);
		y += 40f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"总回合数：{_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.82f, 0.87f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"带出战利品价值：{_lootValue}", HorizontalAlignment.Left, -1f, 16, new Color(0.95f, 0.86f, 0.48f));
		y += 36f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "AI 小队总结", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		foreach (AiSquad squad in _aiSquads)
		{
			string line = $"{squad.Name}：{GetAiIntentLabel(squad.Intent)}，战力 {Mathf.Max(0, squad.Strength)}，战利品 {squad.Loot.Count}";
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), line, HorizontalAlignment.Left, 620f, 14, new Color(0.82f, 0.86f, 0.92f));
			y += 22f;
		}

		Rect2 rect = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(140f, 30f));
		DrawButton(rect, "重新开始", new Color(0.26f, 0.45f, 0.62f));
		_buttons.Add(new ButtonDef(rect, "restart"));
	}

	private static string GetNodeTypeLabel(NodeType type) => type switch
	{
		NodeType.Room => "普通房间",
		NodeType.Battle => "战斗房间",
		NodeType.Search => "搜索房间",
		NodeType.Extract => "撤离点",
		_ => "未知",
	};

	private static string GetContainerKindLabel(ContainerKind kind) => kind switch
	{
		ContainerKind.Room => "房间容器",
		ContainerKind.CorpsePile => "尸体堆",
		ContainerKind.EliteCorpse => "独立尸体",
		_ => "未知",
	};

	private static string GetEquipmentSlotLabel(EquipmentSlot slot) => slot switch
	{
		EquipmentSlot.Weapon => "武器",
		EquipmentSlot.Armor => "护甲",
		EquipmentSlot.Trinket => "饰品",
		_ => "槽位",
	};

	private static string GetAiIntentLabel(AiIntent intent) => intent switch
	{
		AiIntent.Idle => "待机",
		AiIntent.Moving => "移动中",
		AiIntent.Clearing => "清图中",
		AiIntent.Looting => "搜刮中",
		AiIntent.Fighting => "交战中",
		AiIntent.Extracting => "前往撤离",
		AiIntent.Extracted => "已撤离",
		AiIntent.Defeated => "已被击败",
		_ => "未知",
	};
	private string GetAiIntentSummary(AiSquad squad) => squad.Intent switch
	{
		AiIntent.Moving => $"前往：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Clearing => $"清理：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Looting => $"搜刮：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Fighting => $"交战：{this.GetRivalName(squad)}",
		AiIntent.Extracting => $"撤离：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Extracted => "已撤离",
		AiIntent.Defeated => "已败退",
		_ => "待机",
	};

	private string GetAiNextActionSummary(AiSquad squad)
	{
		if (!squad.IsAlive)
		{
			return squad.Intent == AiIntent.Extracted ? "已撤离" : "已败退";
		}

		if (squad.BusyTurns > 1)
		{
			return squad.Intent switch
			{
				AiIntent.Clearing => $"继续清图：{this.GetNodeShortName(squad.NodeId)}",
				AiIntent.Looting => $"继续搜刮：{this.GetNodeShortName(squad.NodeId)}",
				AiIntent.Fighting => $"继续交战：{this.GetRivalName(squad)}",
				_ => "继续当前行动",
			};
		}

		if (squad.BusyTurns == 1)
		{
			return squad.Intent switch
			{
				AiIntent.Clearing => "完成清图",
				AiIntent.Looting => "完成搜刮",
				AiIntent.Fighting => "分出胜负",
				_ => "完成当前行动",
			};
		}

		int nextNodeId = this.GetAiPredictedNextNodeId(squad);
		if (nextNodeId >= 0 && nextNodeId != squad.NodeId)
		{
			return $"移动：{this.GetNodeShortName(nextNodeId)}";
		}

		if (squad.Strength <= 3 || squad.Supplies <= 0 || squad.Loot.Count >= 4)
		{
			int extractId = this.FindExtractNode();
			return squad.NodeId == extractId ? "撤离完成" : $"撤离：{this.GetNodeShortName(extractId)}";
		}

		MapNode node = _nodes[squad.NodeId];
		if (node.Threat > 0)
		{
			return $"清图：{this.GetNodeShortName(node.Id)}";
		}

		if (this.CanAiLootNode(node))
		{
			return $"搜刮：{this.GetNodeShortName(node.Id)}";
		}

		return "待机";
	}

	private int GetAiPredictedNextNodeId(AiSquad squad)
	{
		if (!squad.IsAlive || squad.BusyTurns > 0)
		{
			return -1;
		}

		if (squad.Strength <= 3 || squad.Supplies <= 0 || squad.Loot.Count >= 4)
		{
			int extractId = this.FindExtractNode();
			return squad.NodeId == extractId ? -1 : this.FindNextStep(squad.NodeId, extractId);
		}

		MapNode node = _nodes[squad.NodeId];
		if (node.Threat > 0 || this.CanAiLootNode(node))
		{
			return -1;
		}

		int targetId = this.PickAiTargetNode(squad);
		int nextId = this.FindNextStep(squad.NodeId, targetId);
		return nextId == squad.NodeId ? -1 : nextId;
	}

	private string GetNodeShortName(int nodeId)
	{
		if (nodeId < 0 || nodeId >= _nodes.Count)
		{
			return "未知";
		}

		return _nodes[nodeId].Name;
	}

	private string GetRivalName(AiSquad squad)
	{
		if (squad.RivalId < 0 || squad.RivalId >= _aiSquads.Count)
		{
			return "未知";
		}

		return _aiSquads[squad.RivalId].Name;
	}
}
