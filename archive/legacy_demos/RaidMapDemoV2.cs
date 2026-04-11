using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RaidMapDemoV2 : Node2D
{
	private enum NodeType { Room, Battle, Search, Extract }
	private enum ContainerKind { Room, CorpsePile, EliteCorpse }
	private enum AiIntent { Idle, Moving, Clearing, Looting, Fighting, Extracting, Extracted, Defeated }
	private enum ItemRarity { White, Green, Blue, Purple, Gold }
	private enum EquipmentSlot { Weapon, Armor, Trinket }

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
		public Vector2I GridSize;
		public readonly List<LootItem> Items = new();
		public readonly List<EquippedLoot> EquippedItems = new();
		public LootItem ActiveSearchItem;
		public bool IsSearching;
		public bool IsEmpty => CountUntakenItems() == 0;
		public int HiddenRemaining => CountHiddenItems();

		private int CountHiddenItems()
		{
			int count = 0;
			foreach (LootItem item in Items)
			{
				if (!item.Revealed && !item.Taken)
				{
					count++;
				}
			}
			return count;
		}

		private int CountUntakenItems()
		{
			int count = 0;
			foreach (EquippedLoot equipped in EquippedItems)
			{
				if (equipped.Item != null && !equipped.Item.Taken)
				{
					count++;
				}
			}
			foreach (LootItem item in Items)
			{
				if (!item.Taken)
				{
					count++;
				}
			}
			return count;
		}
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
		public bool Taken;
	}

	private sealed class EquippedLoot
	{
		public EquipmentSlot Slot;
		public LootItem Item;
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
	private string _status = "鐐瑰嚮鐩搁偦鑺傜偣绉诲姩銆?;

	public override void _Ready()
	{
		_rng.Randomize();
		ResetRun();
	}

	public override void _Process(double delta)
	{
		UpdateContainerSearch((float)delta);
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

		if (_runEnded || _battleSim != null || _showSearchConfirm || _selectedContainerIndex >= 0)
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
		if (_selectedContainerIndex >= 0)
		{
			DrawContainerPopup();
		}
		if (_showSearchConfirm)
		{
			DrawSearchConfirmDialog();
		}
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

		AddNode(0, "鍏ュ彛澶у巺", NodeType.Room, new Vector2(120f, 360f), 0);
		AddNode(1, "鍌ㄨ棌瀹?, NodeType.Search, new Vector2(280f, 220f), 0);
		AddNode(2, "宀楀摠", NodeType.Battle, new Vector2(290f, 500f), 4);
		AddNode(3, "妗ｆ瀹?, NodeType.Search, new Vector2(470f, 160f), 0);
		AddNode(4, "宀旇矾鍙?, NodeType.Room, new Vector2(470f, 360f), 2);
		AddNode(5, "鍏佃惀", NodeType.Battle, new Vector2(470f, 560f), 5);
		AddNode(6, "瀹濆簱", NodeType.Search, new Vector2(660f, 220f), 3);
		AddNode(7, "鍗椾晶鎾ょ鐐?, NodeType.Extract, new Vector2(650f, 500f), 0);

		LinkNodes(0, 1); LinkNodes(0, 2); LinkNodes(1, 3); LinkNodes(1, 4); LinkNodes(2, 4); LinkNodes(2, 5);
		LinkNodes(3, 4); LinkNodes(3, 6); LinkNodes(4, 5); LinkNodes(4, 6); LinkNodes(4, 7); LinkNodes(5, 7); LinkNodes(6, 7);

		_nodes[0].Visited = true;
		_playerNodeId = 0;

		AddRoomContainer(1, "琛ョ粰绠?, 4, 0);
		AddRoomContainer(1, "宸ュ叿鏌?, 3, 0);
		AddRoomContainer(3, "鏂囨。绠?, 4, 1);
		AddRoomContainer(4, "閬楀純鑳屽寘", 2, 0);
		AddRoomContainer(6, "瀹濈墿鏋?, 5, 2);
		AddRoomContainer(6, "瀹濆簱鎶藉眽", 4, 1);

		_aiSquads.Add(new AiSquad { Name = "璧ょ嫾灏忛槦", NodeId = 6, Strength = 9, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "钃濋甫灏忛槦", NodeId = 5, Strength = 8, Supplies = 2, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "閲戠嫯灏忛槦", NodeId = 3, Strength = 7, Supplies = 3, Intent = AiIntent.Idle });

		LogEvent("琛屽姩寮€濮嬶紝鍏朵粬灏忛槦宸茬粡杩涘叆鍦板浘銆?);
		RefreshStatus();
	}

	private void AddNode(int id, string name, NodeType type, Vector2 position, int threat) => _nodes.Add(new MapNode { Id = id, Name = name, Type = type, Position = position, Threat = threat });
	private void LinkNodes(int a, int b) { _nodes[a].Links.Add(b); _nodes[b].Links.Add(a); }

	private void AddRoomContainer(int nodeId, string label, int hiddenCount, int visibleCount)
	{
		LootContainer container = CreateGridContainer(label, ContainerKind.Room, RollContainerSize(), hiddenCount, visibleCount);
		_nodes[nodeId].Containers.Add(container);
	}

	private LootContainer CreateGridContainer(string label, ContainerKind kind, Vector2I gridSize, int hiddenCount, int visibleCount)
	{
		LootContainer container = new() { Label = label, Kind = kind, GridSize = gridSize };
		for (int i = 0; i < hiddenCount + visibleCount; i++)
		{
			LootItem item = CreateRandomLootItem();
			if (!TryPlaceItem(container, item))
			{
				break;
			}
		}

		int revealCount = 0;
		foreach (LootItem item in container.Items)
		{
			if (revealCount >= visibleCount)
			{
				break;
			}
			item.Revealed = true;
			revealCount++;
		}
		return container;
	}

	private Vector2I RollContainerSize()
	{
		return _rng.Randf() < 0.5f ? new Vector2I(5, 4) : new Vector2I(6, 5);
	}

	private LootItem CreateRandomLootItem()
	{
		ItemRarity rarity = RollRarity();
		Vector2I size = rarity switch
		{
			ItemRarity.White => _rng.Randf() < 0.6f ? new Vector2I(1, 1) : new Vector2I(1, 2),
			ItemRarity.Green => _rng.Randf() < 0.5f ? new Vector2I(1, 2) : new Vector2I(2, 1),
			ItemRarity.Blue => _rng.Randf() < 0.5f ? new Vector2I(2, 2) : new Vector2I(1, 3),
			ItemRarity.Purple => new Vector2I(2, 2),
			_ => new Vector2I(2, 3),
		};

		return new LootItem
		{
			Label = RollLootItem(),
			Rarity = rarity,
			Size = size,
			Value = GetItemValueByRarity(rarity),
			SearchTime = GetSearchTime(rarity),
		};
	}

	private ItemRarity RollRarity()
	{
		float roll = _rng.Randf();
		if (roll < 0.45f) return ItemRarity.White;
		if (roll < 0.72f) return ItemRarity.Green;
		if (roll < 0.88f) return ItemRarity.Blue;
		if (roll < 0.97f) return ItemRarity.Purple;
		return ItemRarity.Gold;
	}

	private static int GetItemValueByRarity(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 5,
		ItemRarity.Green => 8,
		ItemRarity.Blue => 12,
		ItemRarity.Purple => 18,
		_ => 24,
	};

	private static float GetSearchTime(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 0.8f,
		ItemRarity.Green => 1.15f,
		ItemRarity.Blue => 1.55f,
		ItemRarity.Purple => 2.1f,
		_ => 2.8f,
	};

	private bool TryPlaceItem(LootContainer container, LootItem item)
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

	private bool IsAreaFree(LootContainer container, Vector2I cell, Vector2I size)
	{
		foreach (LootItem existing in container.Items)
		{
			bool overlapX = cell.X < existing.Cell.X + existing.Size.X && cell.X + size.X > existing.Cell.X;
			bool overlapY = cell.Y < existing.Cell.Y + existing.Size.Y && cell.Y + size.Y > existing.Cell.Y;
			if (overlapX && overlapY) return false;
		}
		return true;
	}

	private void TryMoveToNode(int nodeId)
	{
		if (!_nodes[_playerNodeId].Links.Contains(nodeId))
		{
			_status = "璇ヨ妭鐐逛笌褰撳墠浣嶇疆涓嶇浉杩炪€?;
			return;
		}

		_playerNodeId = nodeId;
		_nodes[nodeId].Visited = true;
		_searchActions = 0;
		_selectedContainerIndex = -1;
		AdvanceTurn($"绉诲姩鑷?{_nodes[nodeId].Name}銆?);
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
			StartEncounter(node, $"{node.Name}瀹堝啗", node.Threat * 4 + 6, null, node.Type == NodeType.Battle);
			return;
		}

		if (!node.SearchRewardClaimed)
		{
			node.SearchRewardClaimed = true;
			_searchActions = 2;
			_status = $"鎶佃揪 {node.Name}锛岃幏寰?2 娆″厤璐规悳绱㈡満浼氥€?;
		}
		else
		{
			RefreshStatus();
		}

		EnsureSelectedContainer();
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

		_status = $"{node.Name} 鐖嗗彂鎴樻枟銆?;
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
		AdvanceTurn(victory ? $"鍦?{node.Name} 鍙栧緱鑳滃埄銆? : $"鍦?{node.Name} 鎴樿触銆?, _encounter.TurnCost, false);

		if (!victory)
		{
			_playerHp = 0;
			_runEnded = true;
			_runFailed = true;
			_status = "鏈眬缁撴潫銆?;
			LogEvent("鐜╁灏忛槦鍏ㄧ伃銆?);
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
			LogEvent($"{squad.Name} 琚帺瀹跺嚮婧冦€?);
		}
		else
		{
			LogEvent($"{node.Name} 鐨勫畧鍐涜鍑昏触銆?);
		}

		_status = $"{node.Name} 鎴樻枟鑳滃埄锛屽彲浠ュ紑濮嬫悳鍒垬鍒╁搧銆?;
		_encounter = null;
		EnsureSelectedContainer();
	}

	private void GenerateBattleLoot(MapNode node, AiSquad squad)
	{
		LootContainer pile = CreateGridContainer("灏镐綋鍫?, ContainerKind.CorpsePile, new Vector2I(6, 5), _rng.RandiRange(3, 5), 0);
		if (squad != null && squad.Loot.Count > 0)
		{
			int replaceCount = Mathf.Min(squad.Loot.Count, pile.Items.Count);
			for (int i = 0; i < replaceCount; i++)
			{
				pile.Items[i].Label = TakeRandomLoot(squad);
			}
		}
		node.Containers.Add(pile);

		LootContainer elite = CreateGridContainer(squad != null ? $"{squad.Name} 闃熼暱" : "绮捐嫳瀹堝崼", ContainerKind.EliteCorpse, new Vector2I(5, 4), 2, 2);
		if (elite.Items.Count > 0) { elite.Items[0].Label = squad != null ? "绮鹃挗鍐涘垁" : "瀹堝崼闀挎灙"; elite.Items[0].Revealed = true; }
		if (elite.Items.Count > 1) { elite.Items[1].Label = squad != null ? "闃熼暱閿佺敳" : "閽㈢墖鐢?; elite.Items[1].Revealed = true; }
		if (elite.Items.Count > 2) { elite.Items[2].Label = "缁峰甫鍖?; elite.Items[2].Revealed = false; }
		if (elite.Items.Count > 3) { elite.Items[3].Label = squad != null && squad.Loot.Count > 0 ? TakeRandomLoot(squad) : RollLootItem(); elite.Items[3].Revealed = false; }
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
		List<LootItem> equipped = new();
		foreach (LootItem item in elite.Items)
		{
			if (item.Revealed && !item.Taken)
			{
				equipped.Add(item);
			}
		}

		if (equipped.Count > 0)
		{
			elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Weapon, Item = equipped[0] });
		}
		if (equipped.Count > 1)
		{
			elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Armor, Item = equipped[1] });
		}
		if (equipped.Count > 2)
		{
			elite.EquippedItems.Add(new EquippedLoot { Slot = EquipmentSlot.Trinket, Item = equipped[2] });
		}
		else
		{
			elite.EquippedItems.Add(new EquippedLoot
			{
				Slot = EquipmentSlot.Trinket,
				Item = CreateEquippedItem("缁跺甫鍧犻グ", ItemRarity.Purple)
			});
		}

		foreach (LootItem item in equipped)
		{
			elite.Items.Remove(item);
		}
	}

	private LootItem CreateEquippedItem(string label, ItemRarity rarity)
	{
		return new LootItem
		{
			Label = label,
			Rarity = rarity,
			Size = Vector2I.One,
			Cell = Vector2I.Zero,
			Value = GetItemValueByRarity(rarity),
			SearchTime = 0f,
			SearchProgress = 0f,
			Revealed = true
		};
	}

	private void AdvanceTurn(string reason, int amount = 1, bool refreshStatus = true)
	{
		for (int i = 0; i < amount; i++)
		{
			_turn++;
			SimulateAiTurn();
		}

		LogEvent($"绗?{_turn} 鍥炲悎锛歿reason}");
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
					LogEvent($"{squad.Name} 宸叉挙绂伙紝甯﹁蛋浜?{squad.Loot.Count} 浠舵垬鍒╁搧銆?);
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
				LogEvent($"{squad.Name} 寮€濮嬫竻鐞?{node.Name}銆?);
				continue;
			}

			if (CanAiLootNode(node))
			{
				squad.Intent = AiIntent.Looting;
				squad.BusyTurns = 1;
				LogEvent($"{squad.Name} 寮€濮嬫悳鍒?{node.Name}銆?);
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
			LogEvent($"{squad.Name} 娓呯悊浜?{node.Name}锛屾崯澶变簡 {loss} 鐐规垬鍔涖€?);
		}
		else if (squad.Intent == AiIntent.Looting)
		{
			if (TryAiLootNode(squad, node))
			{
				LogEvent($"{squad.Name} 鎼滃埉浜?{node.Name}銆?);
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
				LogEvent($"{a.Name} 涓?{b.Name} 鍦?{_nodes[a.NodeId].Name} 浜ゆ垬銆?);
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

		LootContainer pile = CreateGridContainer($"{loser.Name} 鐨勯仐楠?, ContainerKind.CorpsePile, new Vector2I(5, 4), 3, 0);
		_nodes[winner.NodeId].Containers.Add(pile);
		LogEvent($"{winner.Name} 鍦?{_nodes[winner.NodeId].Name} 鍑昏触浜?{loser.Name}銆?);
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
				if (equipped.Item == null || equipped.Item.Taken)
				{
					continue;
				}

				equipped.Item.Taken = true;
				squad.Loot.Add(equipped.Item.Label);
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}

			foreach (LootItem item in container.Items)
			{
				if (item.Taken)
				{
					continue;
				}

				item.Revealed = true;
				item.Taken = true;
				squad.Loot.Add(item.Label);
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
		LogEvent($"{squad.Name} 绉诲姩鍒颁簡 {_nodes[nextId].Name}銆?);
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
			case "close_container":
				_selectedContainerIndex = -1;
				RefreshStatus();
				break;
			case "extract":
				TryExtract();
				break;
			case "toggle_auto_search":
				_autoSearchEnabled = !_autoSearchEnabled;
				_status = _autoSearchEnabled ? "宸插紑鍚嚜鍔ㄦ悳绱€傛墦寮€瀹瑰櫒鏃朵細鑷姩鎻ず鐗╁搧銆? : "宸插叧闂嚜鍔ㄦ悳绱€?;
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

	private void OpenContainer(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		_selectedContainerIndex = containerIndex;
		if (_autoSearchEnabled && CanSearch(node))
		{
			StartSearchOnNextHiddenItem(containerIndex);
			return;
		}

		RefreshStatus();
	}

	private void TrySearch(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "褰撳墠鎴块棿涓嶅瀹夊叏锛屾棤娉曟悳绱€?;
			return;
		}
		int containerIndex = encodedIndex / 100;
		int itemIndex = encodedIndex % 100;
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		if (itemIndex < 0 || itemIndex >= container.Items.Count)
		{
			return;
		}

		LootItem item = container.Items[itemIndex];
		if (item.Revealed || item.Taken)
		{
			_status = "杩欎釜瀹瑰櫒閲屾病鏈夋湭鎻ず鐗╁搧浜嗐€?;
			return;
		}

		if (TryGetActiveSearch(out LootContainer activeContainer, out LootItem activeItem) && (activeContainer != container || activeItem != item))
		{
			_status = "褰撳墠宸叉湁鐗╁搧姝ｅ湪鎼滅储锛岃绛夊緟瀹屾垚銆?;
			return;
		}

		if (_searchActions <= 0)
		{
			RequestSearchExchange(encodedIndex);
			return;
		}

		_searchActions--;
		_selectedContainerIndex = containerIndex;
		container.ActiveSearchItem = item;
		container.ActiveSearchItem.SearchProgress = 0f;
		container.IsSearching = true;
		LogEvent($"寮€濮嬫悳绱?{container.Label} 涓殑鐗╁搧銆?);
		RefreshStatus();
	}

	private void RequestSearchExchange(int encodedIndex)
	{
		_pendingRevealContainerIndex = encodedIndex;
		_selectedContainerIndex = encodedIndex / 100;
		if (_skipSearchConfirm)
		{
			ConfirmSearchExchange(true);
			return;
		}

		_confirmSkipChecked = false;
		_showSearchConfirm = true;
	}

	private void ConfirmSearchExchange(bool confirmed)
	{
		_showSearchConfirm = false;
		if (!confirmed)
		{
			_pendingRevealContainerIndex = -1;
			_status = "宸插彇娑堝厬鎹㈡悳绱㈡鏁般€?;
			return;
		}

		if (_confirmSkipChecked)
		{
			_skipSearchConfirm = true;
		}

		int target = _pendingRevealContainerIndex;
		_pendingRevealContainerIndex = -1;
		AdvanceTurn($"鍦?{_nodes[_playerNodeId].Name} 鑺辫垂鏃堕棿鎼滅储銆?, 1, false);
		_searchActions += 4;
		if (target >= 0)
		{
			TrySearch(target);
		}
	}

	private void StartSearchOnNextHiddenItem(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		if (TryGetActiveSearch(out _, out _))
		{
			return;
		}

		LootItem next = GetNextHiddenItem(node.Containers[containerIndex]);
		if (next == null)
		{
			return;
		}

		int itemIndex = node.Containers[containerIndex].Items.IndexOf(next);
		TrySearch(containerIndex * 100 + itemIndex);
	}

	private static LootItem GetNextHiddenItem(LootContainer container)
	{
		LootItem best = null;
		int bestIndex = int.MaxValue;
		foreach (LootItem item in container.Items)
		{
			if (item.Revealed || item.Taken)
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

	private bool TryGetActiveSearch(out LootContainer activeContainer, out LootItem activeItem)
	{
		MapNode node = _nodes[_playerNodeId];
		foreach (LootContainer container in node.Containers)
		{
			if (!container.IsSearching || container.ActiveSearchItem == null)
			{
				continue;
			}

			activeContainer = container;
			activeItem = container.ActiveSearchItem;
			return true;
		}

		activeContainer = null;
		activeItem = null;
		return false;
	}

	private void TakeVisible(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "鎴块棿浠嶇劧鍗遍櫓锛屾殏鏃朵笉鑳芥悳鍒€?;
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

			LootItem equippedItem = container.EquippedItems[equipIndex].Item;
			if (equippedItem == null || equippedItem.Taken)
			{
				return;
			}

			equippedItem.Taken = true;
			_selectedContainerIndex = containerIndex;
			AddLoot(equippedItem.Label);
			LogEvent($"从 {container.Label} 拿取了 {equippedItem.Label}。");
			RefreshStatus();
			return;
		}

		if (itemIndex < 0 || itemIndex >= container.Items.Count)
		{
			return;
		}

		LootItem item = container.Items[itemIndex];
		if (!item.Revealed || item.Taken)
		{
			return;
		}

		item.Taken = true;
		_selectedContainerIndex = containerIndex;
		AddLoot(item.Label);
		LogEvent($"浠?{container.Label} 鎷垮彇浜?{item.Label}銆?);
		RefreshStatus();
	}

	private void TryExtract()
	{
		if (_nodes[_playerNodeId].Type != NodeType.Extract)
		{
			_status = "褰撳墠浣嶇疆涓嶆槸鎾ょ鐐广€?;
			return;
		}

		_runEnded = true;
		_runFailed = false;
		_status = "鎾ょ鎴愬姛銆?;
		LogEvent("鐜╁鎴愬姛鎾ょ銆?);
	}

	private bool CanSearch(MapNode node)
	{
		return _battleSim == null && !_showSearchConfirm && node.Threat <= 0 && GetSquadAtNode(node.Id) == null;
	}

	private void AddLoot(string item)
	{
		_lootValue += GetItemValue(item);
	}

	private int GetItemValue(string item)
	{
		if (item.Contains("閬楃墿") || item.Contains("寰借") || item.Contains("瀹濈煶")) return 18;
		if (item.Contains("閿佺敳") || item.Contains("鍐涘垁") || item.Contains("闀挎灙")) return 12;
		if (item.Contains("鍖?) || item.Contains("鍙ｇ伯")) return 7;
		return 5;
	}

	private string RollLootItem()
	{
		string[] items = ["鍙ゆ棫閬楃墿", "閾惰川瀹濈煶", "鍙ｇ伯鍖?, "鑽夎嵂鍖?, "灏佸嵃寰借", "璐告槗璐︽湰", "鐢熼攬閽ュ寵", "鐏补", "缁嗗竷鍗?];
		return items[_rng.RandiRange(0, items.Length - 1)];
	}

	private string RollVisibleEquipment()
	{
		string[] items = ["浣ｅ叺闀垮墤", "鐨敳鑳屽績", "鐚庡紦", "濉旂浘", "閽㈢洈"];
		return items[_rng.RandiRange(0, items.Length - 1)];
	}

	private int CountNodeLoot(MapNode node)
	{
		int count = 0;
		foreach (LootContainer container in node.Containers)
		{
			foreach (EquippedLoot equipped in container.EquippedItems)
			{
				if (equipped.Item != null && !equipped.Item.Taken)
				{
					count++;
				}
			}

			foreach (LootItem item in container.Items)
			{
				if (!item.Taken)
				{
					count++;
				}
			}
		}
		return count;
	}

	private void EnsureSelectedContainer()
	{
		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex >= 0 && _selectedContainerIndex < node.Containers.Count && !node.Containers[_selectedContainerIndex].IsEmpty)
		{
			return;
		}

		_selectedContainerIndex = -1;
	}

	private static int CountRevealedUntaken(LootContainer container)
	{
		int count = 0;
		foreach (EquippedLoot equipped in container.EquippedItems)
		{
			if (equipped.Item != null && equipped.Item.Revealed && !equipped.Item.Taken)
			{
				count++;
			}
		}
		foreach (LootItem item in container.Items)
		{
			if (item.Revealed && !item.Taken)
			{
				count++;
			}
		}
		return count;
	}

	private void RefreshStatus()
	{
		EnsureSelectedContainer();
		MapNode node = _nodes[_playerNodeId];
		AiSquad enemy = GetSquadAtNode(node.Id);
		if (enemy != null)
		{
			_status = $"褰撳墠鑺傜偣瀛樺湪鏁屽灏忛槦锛歿enemy.Name}銆?;
			return;
		}
		if (node.Threat > 0)
		{
			_status = $"{node.Name} 浠嶆湁瀹堝啗椹诲畧銆?;
			return;
		}
		if (node.Type == NodeType.Extract)
		{
			_status = "杩欓噷鏄挙绂荤偣銆?;
			return;
		}
		_status = $"{node.Name} 褰撳墠瀹夊叏锛屽墿浣欐悳绱㈡満浼氾細{_searchActions}銆?;
	}

	private void LogEvent(string text)
	{
		_eventLog.Add(text);
		if (_eventLog.Count > 12)
		{
			_eventLog.RemoveAt(0);
		}
	}

	private void UpdateContainerSearch(float dt)
	{
		if (_battleSim != null || _showSearchConfirm)
		{
			return;
		}

		foreach (MapNode node in _nodes)
		{
			foreach (LootContainer container in node.Containers)
			{
				if (!container.IsSearching || container.ActiveSearchItem == null)
				{
					continue;
				}

				container.ActiveSearchItem.SearchProgress += dt;
				if (container.ActiveSearchItem.SearchProgress < container.ActiveSearchItem.SearchTime)
				{
					continue;
				}

				container.ActiveSearchItem.Revealed = true;
				container.ActiveSearchItem.SearchProgress = container.ActiveSearchItem.SearchTime;
				LogEvent($"鎼滅储瀹屾垚锛屽彂鐜颁簡 {container.ActiveSearchItem.Label}銆?);
				container.ActiveSearchItem = null;
				container.IsSearching = false;

				if (_autoSearchEnabled)
				{
					int containerIndex = node.Containers.IndexOf(container);
					StartSearchOnNextHiddenItem(containerIndex);
				}
			}
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
			if (node.Id == _playerNodeId) DrawCircle(node.Position, 9f, new Color(0.55f, 0.95f, 1f));
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
		MapNode node = _nodes[_playerNodeId];
		EnsureSelectedContainer();

		float x = _sideRect.Position.X + 18f;
		float y = _sideRect.Position.Y + 28f;
		float panelBottom = _sideRect.End.Y - 18f;

		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "鑺傜偣绐佽 Demo", HorizontalAlignment.Left, -1f, 20, Colors.White);
		y += 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"鍥炲悎 {_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.76f, 0.84f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"鐢熷懡 {_playerHp}/{_playerMaxHp}   鎴樺姏 {_playerStrength}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 22f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"鎼滅储 {_searchActions}   鎴樺埄鍝佷环鍊?{_lootValue}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 26f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, 320f, 14, new Color(0.86f, 0.9f, 0.95f));
		y += 52f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"褰撳墠鑺傜偣锛歿node.Name}", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"绫诲瀷锛歿GetNodeTypeLabel(node.Type)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 18f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"濞佽儊锛歿node.Threat}   鎴樺埄鍝侊細{CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 26f;

		Rect2 autoRect = new(new Vector2(x, y), new Vector2(148f, 28f));
		DrawButton(autoRect, _autoSearchEnabled ? "鑷姩鎼滅储锛氬紑" : "鑷姩鎼滅储锛氬叧", _autoSearchEnabled ? new Color(0.24f, 0.56f, 0.32f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(autoRect, "toggle_auto_search"));
		if (node.Type == NodeType.Extract && _battleSim == null && !_runEnded)
		{
			Rect2 extractRect = new(new Vector2(x + 170f, y), new Vector2(124f, 28f));
			DrawButton(extractRect, "鎵ц鎾ょ", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(extractRect, "extract"));
		}
		y += 40f;

		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "瀹瑰櫒", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 18f;
		float listStartY = y;
		int visibleCount = Mathf.Min(node.Containers.Count, 6);
		for (int i = 0; i < node.Containers.Count && i < 6; i++)
		{
			LootContainer container = node.Containers[i];
			Rect2 rowRect = new(new Vector2(x, listStartY + i * 30f), new Vector2(320f, 24f));
			Color fill = i == _selectedContainerIndex ? new Color(0.26f, 0.3f, 0.38f) : new Color(0.12f, 0.13f, 0.16f);
			DrawRect(rowRect, fill, true);
			DrawRect(rowRect, new Color(0.34f, 0.37f, 0.42f), false, 1f);
			DrawString(ThemeDB.FallbackFont, rowRect.Position + new Vector2(10f, 17f), container.Label, HorizontalAlignment.Left, 165f, 12, Colors.White);
			DrawString(ThemeDB.FallbackFont, rowRect.Position + new Vector2(182f, 17f), $"宸叉樉 {CountRevealedUntaken(container)}", HorizontalAlignment.Left, 54f, 11, new Color(0.78f, 0.87f, 0.98f));
			DrawString(ThemeDB.FallbackFont, rowRect.Position + new Vector2(232f, 17f), $"鏈樉 {container.HiddenRemaining}", HorizontalAlignment.Left, 60f, 11, new Color(0.92f, 0.84f, 0.6f));
			Rect2 openRect = new(new Vector2(rowRect.End.X - 68f, rowRect.Position.Y + 2f), new Vector2(56f, 20f));
			DrawButton(openRect, "鎵撳紑", new Color(0.23f, 0.4f, 0.58f));
			_buttons.Add(new ButtonDef(openRect, "open_container", i));
		}

		float detailY = y + visibleCount * 30f;
		float logTop = Mathf.Max(detailY + 18f, panelBottom - 120f);
		DrawLine(new Vector2(x, logTop - 10f), new Vector2(_sideRect.End.X - 18f, logTop - 10f), new Color(0.24f, 0.27f, 0.31f), 1f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logTop), "涓栫晫鍔ㄦ€?, HorizontalAlignment.Left, -1f, 15, Colors.White);
		float logY = logTop + 18f;
		int startIndex = Mathf.Max(0, _eventLog.Count - 5);
		for (int i = startIndex; i < _eventLog.Count; i++)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), _eventLog[i], HorizontalAlignment.Left, 320f, 12, new Color(0.8f, 0.84f, 0.9f));
			logY += 18f;
		}
	}

	private void DrawContainerPopup()
	{
		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex < 0 || _selectedContainerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[_selectedContainerIndex];
		Vector2 cellSize = new(26f, 26f);
		float equipmentHeight = container.Kind == ContainerKind.EliteCorpse ? 92f : 0f;
		Vector2 panelSize = new(container.GridSize.X * cellSize.X + 38f, container.GridSize.Y * cellSize.Y + 72f + equipmentHeight);
		panelSize.X = Mathf.Max(panelSize.X, 260f);
		Rect2 panel = new((GetViewportRect().Size - panelSize) / 2f, panelSize);
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.38f), true);
		DrawRect(panel, new Color(0.08f, 0.09f, 0.11f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(12f, 20f), container.Label, HorizontalAlignment.Left, 180f, 14, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(160f, 20f), GetContainerKindLabel(container.Kind), HorizontalAlignment.Left, 80f, 12, new Color(0.92f, 0.84f, 0.6f));
		Rect2 closeRect = new(new Vector2(panel.End.X - 30f, panel.Position.Y + 8f), new Vector2(20f, 20f));
		DrawRect(closeRect, new Color(0.26f, 0.14f, 0.14f), true);
		DrawRect(closeRect, Colors.White, false, 1.2f);
		DrawString(ThemeDB.FallbackFont, closeRect.Position + new Vector2(6f, 15f), "X", HorizontalAlignment.Left, -1f, 12, Colors.White);
		_buttons.Add(new ButtonDef(closeRect, "close_container"));

		Vector2 gridOrigin = panel.Position + new Vector2(12f, 34f + equipmentHeight);
		if (container.Kind == ContainerKind.EliteCorpse)
		{
			DrawEliteEquipmentSection(container, panel.Position + new Vector2(12f, 36f));
		}

		for (int gridY = 0; gridY < container.GridSize.Y; gridY++)
		{
			for (int gridX = 0; gridX < container.GridSize.X; gridX++)
			{
				Rect2 cellRect = new(gridOrigin + new Vector2(gridX * cellSize.X, gridY * cellSize.Y), cellSize - new Vector2(2f, 2f));
				DrawRect(cellRect, new Color(0.09f, 0.1f, 0.12f), true);
				DrawRect(cellRect, new Color(0.24f, 0.26f, 0.3f), false, 1f);
			}
		}

		foreach (LootItem item in container.Items)
		{
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

				if (!container.IsSearching || container.ActiveSearchItem == item)
				{
					int itemIndex = container.Items.IndexOf(item);
					_buttons.Add(new ButtonDef(itemRect, "search", _selectedContainerIndex * 100 + itemIndex));
				}
			}
			else
			{
				DrawRect(itemRect, GetRarityColor(item.Rarity), true);
				DrawRect(itemRect, Colors.White, false, 1.2f);
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(4f, 16f), item.Label, HorizontalAlignment.Left, itemRect.Size.X - 6f, 10, Colors.Black);
				int itemIndex = container.Items.IndexOf(item);
				_buttons.Add(new ButtonDef(itemRect, "take", _selectedContainerIndex * 100 + itemIndex));
			}

			if (container.ActiveSearchItem == item && !item.Revealed)
			{
				Vector2 center = itemRect.GetCenter();
				float ratio = Mathf.Clamp(item.SearchProgress / item.SearchTime, 0f, 0.98f);
				float endAngle = -Mathf.Pi / 2f + Mathf.Tau * ratio;
				DrawArc(center, 10f, -Mathf.Pi / 2f, endAngle, 24, GetRarityColor(item.Rarity), 3f);
			}
		}
	}

	private void DrawEliteEquipmentSection(LootContainer container, Vector2 origin)
	{
		DrawString(ThemeDB.FallbackFont, origin, "瑁呭鏍?, HorizontalAlignment.Left, -1f, 13, new Color(0.9f, 0.92f, 0.96f));
		float slotY = origin.Y + 12f;
		float slotWidth = 96f;
		float gap = 10f;
		for (int i = 0; i < 3; i++)
		{
			Rect2 slotRect = new(new Vector2(origin.X + i * (slotWidth + gap), slotY + 10f), new Vector2(slotWidth, 44f));
			DrawRect(slotRect, new Color(0.1f, 0.11f, 0.14f), true);
			DrawRect(slotRect, new Color(0.32f, 0.35f, 0.4f), false, 1.2f);
			DrawString(ThemeDB.FallbackFont, slotRect.Position + new Vector2(6f, 14f), GetEquipmentSlotLabel((EquipmentSlot)i), HorizontalAlignment.Left, slotRect.Size.X - 12f, 11, new Color(0.78f, 0.82f, 0.88f));

			EquippedLoot equipped = i < container.EquippedItems.Count ? container.EquippedItems[i] : null;
			if (equipped?.Item == null || equipped.Item.Taken)
			{
				DrawString(ThemeDB.FallbackFont, slotRect.Position + new Vector2(6f, 33f), "绌?, HorizontalAlignment.Left, slotRect.Size.X - 12f, 12, new Color(0.45f, 0.48f, 0.52f));
				continue;
			}

			Rect2 itemRect = new(slotRect.Position + new Vector2(4f, 18f), new Vector2(slotRect.Size.X - 8f, 20f));
			DrawRect(itemRect, GetRarityColor(equipped.Item.Rarity), true);
			DrawRect(itemRect, Colors.White, false, 1.2f);
			DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(4f, 14f), equipped.Item.Label, HorizontalAlignment.Left, itemRect.Size.X - 6f, 10, Colors.Black);
			_buttons.Add(new ButtonDef(itemRect, "take", _selectedContainerIndex * 100 + 50 + i));
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
			if (xOnBottom >= left && xOnBottom <= right)
			{
				intersections.Add(new Vector2(xOnBottom, bottom));
			}

			float xOnTop = c - top;
			if (xOnTop >= left && xOnTop <= right)
			{
				intersections.Add(new Vector2(xOnTop, top));
			}

			float yOnLeft = c - left;
			if (yOnLeft >= top && yOnLeft <= bottom)
			{
				AddUniquePoint(intersections, new Vector2(left, yOnLeft));
			}

			float yOnRight = c - right;
			if (yOnRight >= top && yOnRight <= bottom)
			{
				AddUniquePoint(intersections, new Vector2(right, yOnRight));
			}

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

	private void DrawSearchConfirmDialog()
	{
		Rect2 panel = new(new Vector2(360f, 240f), new Vector2(480f, 210f));
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.45f), true);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.98f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 32f), "鍏戞崲鎼滅储娆℃暟", HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 68f), "褰撳墠鎼滅储娆℃暟涓嶈冻銆傛槸鍚﹁姳璐?1 鍥炲悎鎹㈠彇 4 娆℃悳绱紝", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 90f), "骞剁珛鍒绘彮绀鸿繖浠舵湭鏄庣墿鍝侊紵", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));

		Rect2 checkRect = new(new Vector2(panel.Position.X + 24f, panel.Position.Y + 126f), new Vector2(18f, 18f));
		DrawRect(checkRect, new Color(0.08f, 0.08f, 0.1f), true);
		DrawRect(checkRect, Colors.White, false, 1.2f);
		if (_confirmSkipChecked)
		{
			DrawLine(checkRect.Position + new Vector2(3f, 9f), checkRect.Position + new Vector2(7f, 14f), new Color(0.56f, 0.95f, 0.62f), 2f);
			DrawLine(checkRect.Position + new Vector2(7f, 14f), checkRect.Position + new Vector2(15f, 3f), new Color(0.56f, 0.95f, 0.62f), 2f);
		}
		_buttons.Add(new ButtonDef(checkRect, "toggle_confirm_skip"));
		DrawString(ThemeDB.FallbackFont, checkRect.Position + new Vector2(28f, 14f), "鏈鎺㈢储鍚庣画涓嶅啀鎻愮ず", HorizontalAlignment.Left, -1f, 13, Colors.White);

		Rect2 yesRect = new(new Vector2(panel.Position.X + 24f, panel.End.Y - 46f), new Vector2(90f, 28f));
		Rect2 noRect = new(new Vector2(panel.Position.X + 126f, panel.End.Y - 46f), new Vector2(90f, 28f));
		DrawButton(yesRect, "纭", new Color(0.24f, 0.56f, 0.34f));
		DrawButton(noRect, "鍙栨秷", new Color(0.38f, 0.22f, 0.22f));
		_buttons.Add(new ButtonDef(yesRect, "confirm_search_yes"));
		_buttons.Add(new ButtonDef(noRect, "confirm_search_no"));
	}

	private void DrawButton(Rect2 rect, string text, Color color)
	{
		DrawRect(rect, color, true);
		DrawRect(rect, Colors.White, false, 1.4f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(8f, 17f), text, HorizontalAlignment.Left, rect.Size.X - 12f, 12, Colors.White);
	}

	private void DrawEndOverlay()
	{
		Rect2 panel = new(new Vector2(250f, 170f), new Vector2(720f, 330f));
		DrawRect(panel, new Color(0.01f, 0.01f, 0.02f, 0.96f), true);
		DrawRect(panel, Colors.White, false, 2f);
		float x = panel.Position.X + 26f;
		float y = panel.Position.Y + 38f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _runFailed ? "琛屽姩澶辫触" : "鎾ょ瀹屾垚", HorizontalAlignment.Left, -1f, 24, Colors.White);
		y += 40f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"鎬诲洖鍚堟暟锛歿_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.82f, 0.87f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"甯﹀嚭鎴樺埄鍝佷环鍊硷細{_lootValue}", HorizontalAlignment.Left, -1f, 16, new Color(0.95f, 0.86f, 0.48f));
		y += 36f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "AI 灏忛槦鎬荤粨", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		foreach (AiSquad squad in _aiSquads)
		{
			string line = $"{squad.Name}锛歿GetAiIntentLabel(squad.Intent)}锛屾垬鍔?{Mathf.Max(0, squad.Strength)}锛屾垬鍒╁搧 {squad.Loot.Count}";
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), line, HorizontalAlignment.Left, 620f, 14, new Color(0.82f, 0.86f, 0.92f));
			y += 22f;
		}
		Rect2 restartRect = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(140f, 30f));
		DrawButton(restartRect, "閲嶆柊寮€濮?, new Color(0.26f, 0.45f, 0.62f));
		_buttons.Add(new ButtonDef(restartRect, "restart"));
	}

	private static string GetNodeTypeLabel(NodeType type) => type switch
	{
		NodeType.Room => "鏅€氭埧闂?,
		NodeType.Battle => "鎴樻枟鎴块棿",
		NodeType.Search => "鎼滅储鎴块棿",
		NodeType.Extract => "鎾ょ鐐?,
		_ => "鏈煡",
	};

	private static string GetContainerKindLabel(ContainerKind kind) => kind switch
	{
		ContainerKind.Room => "鎴块棿瀹瑰櫒",
		ContainerKind.CorpsePile => "灏镐綋鍫?,
		ContainerKind.EliteCorpse => "鐙珛灏镐綋",
		_ => "鏈煡",
	};

	private static string GetEquipmentSlotLabel(EquipmentSlot slot) => slot switch
	{
		EquipmentSlot.Weapon => "濮濓箑娅?,
		EquipmentSlot.Armor => "閹躲倗鏁?,
		EquipmentSlot.Trinket => "妤楁澘鎼?,
		_ => "濡叉垝缍?,
	};

	private static Color GetRarityColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => new Color(0.82f, 0.84f, 0.87f),
		ItemRarity.Green => new Color(0.48f, 0.85f, 0.45f),
		ItemRarity.Blue => new Color(0.36f, 0.7f, 1f),
		ItemRarity.Purple => new Color(0.72f, 0.48f, 0.92f),
		_ => new Color(1f, 0.82f, 0.32f),
	};

	private static string GetAiIntentLabel(AiIntent intent) => intent switch
	{
		AiIntent.Idle => "寰呮満",
		AiIntent.Moving => "绉诲姩涓?,
		AiIntent.Clearing => "娓呭浘涓?,
		AiIntent.Looting => "鎼滃埉涓?,
		AiIntent.Fighting => "浜ゆ垬涓?,
		AiIntent.Extracting => "鍓嶅線鎾ょ",
		AiIntent.Extracted => "宸叉挙绂?,
		AiIntent.Defeated => "宸茶鍑昏触",
		_ => "鏈煡",
	};
}
