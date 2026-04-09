using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RaidMapDemo : Node2D
{
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

	private sealed class MapNode
	{
		public int Id;
		public string Name = "";
		public NodeType Type;
		public Vector2 Position;
		public readonly List<int> Links = new();
		public int Threat;
		public bool Visited;
		public bool SearchRewardClaimed;
		public readonly List<LootContainer> Containers = new();
	}

	private sealed class LootContainer
	{
		public string Label = "";
		public ContainerKind Kind;
		public readonly List<string> VisibleItems = new();
		public readonly List<string> HiddenItems = new();
		public readonly List<EquippedLoot> EquippedItems = new();
		public int HiddenIndex;
		public bool IsEmpty => VisibleItems.Count == 0 && HiddenIndex >= HiddenItems.Count && CountUntakenEquippedItems() == 0;
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
	}

	private sealed class EquippedLoot
	{
		public EquipmentSlot Slot;
		public string Label = "";
		public bool Taken;
	}

	private sealed class AiSquad
	{
		public string Name = "";
		public int NodeId;
		public int Strength;
		public int Supplies;
		public AiIntent Intent;
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
	private readonly RandomNumberGenerator _rng = new();

	private readonly Rect2 _mapRect = new(new Vector2(30f, 30f), new Vector2(760f, 660f));
	private readonly Rect2 _sideRect = new(new Vector2(810f, 30f), new Vector2(360f, 660f));

	private Encounter _encounter;
	private RoomBattleSim _battleSim;
	private int _playerNodeId;
	private int _turn;
	private int _playerHp;
	private int _playerMaxHp;
	private int _playerStrength;
	private int _searchActions;
	private int _lootValue;
	private bool _runEnded;
	private bool _runFailed;
	private bool _autoSearchEnabled;
	private bool _skipSearchConfirm;
	private bool _showSearchConfirm;
	private bool _confirmSkipChecked;
	private int _selectedContainerIndex = -1;
	private int _pendingRevealContainerIndex = -1;
	private string _status = "点击相邻节点移动。";

	public override void _Ready()
	{
		_rng.Randomize();
		ResetRun();
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
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

		if (_runEnded || _battleSim != null)
		{
			return;
		}

		foreach (MapNode node in _nodes)
		{
			if (node.Position.DistanceTo(click) <= 24f)
			{
				TryMoveToNode(node.Id);
				return;
			}
		}
	}

	public override void _Draw()
	{
		_buttons.Clear();
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0.06f, 0.07f, 0.09f), true);
		DrawMap();
		DrawSidePanel();
		if (_runEnded)
		{
			DrawEndOverlay();
		}
	}

	private void ResetRun()
	{
		_nodes.Clear();
		_aiSquads.Clear();
		_eventLog.Clear();

		_playerMaxHp = 24;
		_playerHp = 24;
		_playerStrength = 11;
		_searchActions = 0;
		_lootValue = 0;
		_turn = 0;
		_runEnded = false;
		_runFailed = false;
		_autoSearchEnabled = false;
		_skipSearchConfirm = false;
		_showSearchConfirm = false;
		_confirmSkipChecked = false;
		_selectedContainerIndex = -1;
		_pendingRevealContainerIndex = -1;
		_encounter = null;

		AddNode(0, "入口大厅", NodeType.Room, new Vector2(120f, 360f), 0);
		AddNode(1, "储藏室", NodeType.Search, new Vector2(280f, 220f), 0);
		AddNode(2, "岗哨", NodeType.Battle, new Vector2(290f, 500f), 4);
		AddNode(3, "档案室", NodeType.Search, new Vector2(470f, 160f), 0);
		AddNode(4, "岔路口", NodeType.Room, new Vector2(470f, 360f), 2);
		AddNode(5, "兵营", NodeType.Battle, new Vector2(470f, 560f), 5);
		AddNode(6, "宝库", NodeType.Search, new Vector2(660f, 220f), 3);
		AddNode(7, "南侧撤离点", NodeType.Extract, new Vector2(650f, 500f), 0);

		LinkNodes(0, 1); LinkNodes(0, 2); LinkNodes(1, 3); LinkNodes(1, 4);
		LinkNodes(2, 4); LinkNodes(2, 5); LinkNodes(3, 4); LinkNodes(3, 6);
		LinkNodes(4, 5); LinkNodes(4, 6); LinkNodes(4, 7); LinkNodes(5, 7); LinkNodes(6, 7);

		_nodes[0].Visited = true;
		_playerNodeId = 0;

		AddRoomContainer(1, "补给箱", 4, 0);
		AddRoomContainer(1, "工具柜", 3, 0);
		AddRoomContainer(3, "文档箱", 4, 1);
		AddRoomContainer(4, "遗弃背包", 2, 0);
		AddRoomContainer(6, "宝物架", 5, 2);
		AddRoomContainer(6, "宝库抽屉", 4, 1);

		_aiSquads.Add(new AiSquad { Name = "赤狼小队", NodeId = 6, Strength = 9, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "蓝鸦小队", NodeId = 5, Strength = 8, Supplies = 2, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "金狮小队", NodeId = 3, Strength = 7, Supplies = 3, Intent = AiIntent.Idle });

		LogEvent("行动开始，其他小队已经进入地图。");
		RefreshStatus();
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

	private void TryMoveToNode(int nodeId)
	{
		MapNode current = _nodes[_playerNodeId];
		if (!current.Links.Contains(nodeId))
		{
			_status = "该节点与当前位置不相连。";
			return;
		}

		_playerNodeId = nodeId;
		_nodes[nodeId].Visited = true;
		_searchActions = 0;
		AdvanceTurn($"移动至 {_nodes[nodeId].Name}。");
		HandleArrival(_nodes[nodeId]);
	}

	private void HandleArrival(MapNode node)
	{
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

		if (!node.SearchRewardClaimed)
		{
			node.SearchRewardClaimed = true;
			_searchActions = 2;
			_status = $"抵达 {node.Name}，获得 2 次免费搜索机会。";
		}
		else
		{
			RefreshStatus();
		}
	}

	private void StartEncounter(MapNode node, string enemyName, int enemyPower, AiSquad squad, bool hasElite)
	{
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
		_battleSim.Setup(node.Name, _playerHp, _playerStrength, enemyPower, hasElite, enemyName);
	}

	private void OnBattleFinished(bool victory, int remainingHp, int remainingStrength)
	{
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

		if (!victory)
		{
			_playerHp = 0;
			_runEnded = true;
			_runFailed = true;
			_status = "本局结束。";
			LogEvent("玩家小队全灭。");
			_encounter = null;
			return;
		}

		_playerHp = Mathf.Clamp(remainingHp, 4, _playerMaxHp);
		_playerStrength = Mathf.Max(4, remainingStrength);
		node.Threat = 0;
		GenerateBattleLoot(node, squad);
		_searchActions = 0;

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

	private void AdvanceTurn(string reason, int amount = 1, bool refreshStatus = true)
	{
		for (int i = 0; i < amount; i++)
		{
			_turn++;
			SimulateAiTurn();
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
				squad.BusyTurns = _rng.RandiRange(1, 2);
				LogEvent($"{squad.Name} 开始清理 {node.Name}。");
				continue;
			}

			if (CanAiLootNode(node))
			{
				squad.Intent = AiIntent.Looting;
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
				a.BusyTurns = _rng.RandiRange(3, 5);
				b.BusyTurns = a.BusyTurns;
				a.RivalId = j;
				b.RivalId = i;
				LogEvent($"{a.Name} 与 {b.Name} 在 {_nodes[a.NodeId].Name} 交战。");
			}
		}
	}

	private void ResolveAiDuel(AiSquad squad)
	{
		if (squad.RivalId < 0 || squad.RivalId >= _aiSquads.Count)
		{
			squad.Intent = AiIntent.Idle;
			return;
		}

		AiSquad rival = _aiSquads[squad.RivalId];
		if (!rival.IsAlive)
		{
			squad.Intent = AiIntent.Idle;
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
		winner.BusyTurns = 0;
		winner.RivalId = -1;
		loser.Strength = 0;
		loser.Intent = AiIntent.Defeated;
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
			return;
		}
		squad.NodeId = nextId;
		squad.Intent = intent;
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
		if (_runEnded)
		{
			if (button.Action == "restart")
			{
				ResetRun();
			}
			return;
		}

		switch (button.Action)
		{
			case "search":
				TrySearch(button.Index);
				break;
			case "take":
				TakeVisible(button.Index);
				break;
			case "open_container":
				OpenContainer(button.Index);
				break;
			case "extract":
				TryExtract();
				break;
			case "toggle_auto_search":
				_autoSearchEnabled = !_autoSearchEnabled;
				_status = _autoSearchEnabled ? "已开启自动搜索。打开容器时会自动揭示物品。" : "已关闭自动搜索。";
				break;
			case "confirm_search_yes":
				ConfirmSearchExchange(true);
				break;
			case "confirm_search_no":
				ConfirmSearchExchange(false);
				break;
			case "toggle_confirm_skip":
				_confirmSkipChecked = !_confirmSkipChecked;
				break;
		}
	}

	private void TrySearch(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "当前房间不够安全，无法搜索。";
			return;
		}
		if (_searchActions <= 0)
		{
			_status = "搜索机会不足，可花费 1 回合补充。";
			return;
		}
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		if (container.HiddenRemaining <= 0)
		{
			_status = "这个容器里没有未检视物品了。";
			return;
		}

		string item = container.HiddenItems[container.HiddenIndex];
		container.HiddenIndex++;
		_searchActions--;
		AddLoot(item);
		LogEvent($"搜索 {container.Label}，发现了 {item}。");
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
		if (_autoSearchEnabled && CanSearch(node) && node.Containers[containerIndex].HiddenRemaining > 0)
		{
			TrySearch(containerIndex);
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

		_runEnded = true;
		_runFailed = false;
		_status = "撤离成功。";
		LogEvent("玩家成功撤离。");
	}

	private bool CanSearch(MapNode node)
	{
		return _battleSim == null && node.Threat <= 0 && GetSquadAtNode(node.Id) == null;
	}

	private void AddLoot(string item)
	{
		_lootValue += GetItemValue(item);
	}

	private int GetItemValue(string item)
	{
		if (item.Contains("遗物") || item.Contains("徽记") || item.Contains("宝石")) return 18;
		if (item.Contains("锁甲") || item.Contains("军刀") || item.Contains("长枪")) return 12;
		if (item.Contains("包") || item.Contains("口粮")) return 7;
		return 5;
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

			count += container.VisibleItems.Count + container.HiddenRemaining;
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
		_status = $"{node.Name} 当前安全，剩余搜索机会：{_searchActions}。";
	}

	private void LogEvent(string text)
	{
		_eventLog.Add(text);
		if (_eventLog.Count > 12)
		{
			_eventLog.RemoveAt(0);
		}
	}

	private void DrawMap()
	{
		DrawRect(_mapRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(_mapRect, new Color(0.28f, 0.31f, 0.36f), false, 2f);

		foreach (MapNode node in _nodes)
		{
			foreach (int link in node.Links)
			{
				if (link < node.Id) continue;
				DrawLine(node.Position, _nodes[link].Position, new Color(0.28f, 0.32f, 0.36f), 3f);
			}
		}

		foreach (MapNode node in _nodes)
		{
			Color color = GetNodeColor(node);
			DrawCircle(node.Position, 22f, color);
			DrawArc(node.Position, 28f, 0f, Mathf.Tau, 32, new Color(color.R, color.G, color.B, 0.45f), 2f);
			DrawString(ThemeDB.FallbackFont, node.Position + new Vector2(-42f, -32f), node.Name, HorizontalAlignment.Left, -1f, 14, Colors.White);
			if (node.Id == _playerNodeId)
			{
				DrawCircle(node.Position, 9f, new Color(0.55f, 0.95f, 1f));
			}
			AiSquad squad = GetSquadAtNode(node.Id);
			if (squad != null)
			{
				DrawCircle(node.Position + new Vector2(18f, 18f), 8f, new Color(0.95f, 0.45f, 0.45f));
				DrawString(ThemeDB.FallbackFont, node.Position + new Vector2(12f, 42f), squad.Name, HorizontalAlignment.Left, -1f, 10, new Color(1f, 0.85f, 0.85f));
			}
		}
	}

	private Color GetNodeColor(MapNode node)
	{
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
		float contentBottom = panelBottom - 170f;
		bool clipped = false;
		MapNode node = _nodes[_playerNodeId];

		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "节点突袭 Demo", HorizontalAlignment.Left, -1f, 20, Colors.White);
		y += 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"回合 {_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.76f, 0.84f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"生命 {_playerHp}/{_playerMaxHp}   战力 {_playerStrength}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 22f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"搜索 {_searchActions}   战利品价值 {_lootValue}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 26f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, 320f, 14, new Color(0.86f, 0.9f, 0.95f));
		y += 54f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"当前节点：{node.Name}", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"类型：{GetNodeTypeLabel(node.Type)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 18f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"威胁：{node.Threat}   战利品：{CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 28f;

		if (node.Type == NodeType.Extract && _battleSim == null && !_runEnded && y + 34f <= contentBottom)
		{
			Rect2 rect = new(new Vector2(x, y), new Vector2(140f, 30f));
			DrawButton(rect, "执行撤离", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(rect, "extract"));
			y += 42f;
		}

		if (CanSearch(node) && CountNodeLoot(node) > 0 && y + 24f <= contentBottom)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "容器", HorizontalAlignment.Left, -1f, 16, Colors.White);
			y += 22f;
			for (int i = 0; i < node.Containers.Count && !clipped; i++)
			{
				LootContainer container = node.Containers[i];
				if (container.IsEmpty) continue;
				float cardHeight = GetContainerCardHeight(container);
				if (y + cardHeight > contentBottom) { clipped = true; break; }
				DrawContainerCard(container, i, x, ref y);
				y += 10f;
			}

			if (!clipped && _searchActions <= 0 && y + 30f <= contentBottom)
			{
				Rect2 rect = new(new Vector2(x, y), new Vector2(220f, 28f));
				DrawButton(rect, "花费 1 回合换取 4 次搜索", new Color(0.46f, 0.25f, 0.19f));
				_buttons.Add(new ButtonDef(rect, "buy_search"));
				y += 38f;
			}
		}

		if (clipped)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, contentBottom - 6f), "下方内容已折叠，后续再做滚动列表。", HorizontalAlignment.Left, 320f, 12, new Color(0.95f, 0.76f, 0.52f));
		}

		float logY = Mathf.Max(y + 12f, contentBottom + 8f);
		DrawLine(new Vector2(x, logY - 10f), new Vector2(_sideRect.End.X - 18f, logY - 10f), new Color(0.24f, 0.27f, 0.31f), 1f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), "世界动态", HorizontalAlignment.Left, -1f, 16, Colors.White);
		logY += 20f;
		int maxLogLines = Mathf.Max(2, (int)((panelBottom - logY) / 18f));
		int startIndex = Mathf.Max(0, _eventLog.Count - maxLogLines);
		for (int i = startIndex; i < _eventLog.Count; i++)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), _eventLog[i], HorizontalAlignment.Left, 320f, 12, new Color(0.8f, 0.84f, 0.9f));
			logY += 18f;
			if (logY > panelBottom) break;
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

	private void DrawEndOverlay()
	{
		Rect2 panel = new(new Vector2(250f, 170f), new Vector2(720f, 330f));
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
}
