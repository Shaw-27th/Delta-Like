using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class NodeRaidDemo : Node2D
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
		FightingSquad,
		Extracting,
		Extracted,
		Defeated,
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
		public bool SearchConsumed;
		public bool SearchTokensGranted;
		public bool Cleared;
		public bool SearchedOut;
		public bool ContainsPlayer;
		public readonly List<LootContainer> Containers = new();
	}

	private sealed class LootContainer
	{
		public string Label = "";
		public ContainerKind Kind;
		public readonly List<string> VisibleItems = new();
		public readonly List<string> HiddenItems = new();
		public int RevealIndex;
		public bool IsEmpty => VisibleItems.Count == 0 && RevealIndex >= HiddenItems.Count;
		public int HiddenRemaining => HiddenItems.Count - RevealIndex;
	}

	private sealed class AiSquad
	{
		public string Name = "";
		public int CurrentNodeId;
		public int Strength;
		public int Supplies;
		public int BusyTurns;
		public int TargetNodeId = -1;
		public int RivalSquadId = -1;
		public AiIntent Intent;
		public readonly List<string> Loot = new();
		public bool IsAlive => Intent != AiIntent.Defeated && Intent != AiIntent.Extracted && Strength > 0;
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

	private sealed class EncounterState
	{
		public string Name = "";
		public int PlayerHp;
		public int EnemyHp;
		public int TurnCost;
		public AiSquad EnemySquad;
		public MapNode Node;
		public readonly List<string> Log = new();
		public float Timer;
	}

	private readonly List<MapNode> _nodes = new();
	private readonly List<AiSquad> _aiSquads = new();
	private readonly List<ButtonDef> _buttons = new();
	private readonly List<string> _eventLog = new();
	private readonly RandomNumberGenerator _rng = new();

	private readonly Rect2 _mapRect = new(new Vector2(30f, 30f), new Vector2(760f, 660f));
	private readonly Rect2 _sideRect = new(new Vector2(810f, 30f), new Vector2(360f, 660f));

	private EncounterState _encounter;
	private int _playerNodeId;
	private int _turn;
	private int _playerHp;
	private int _playerMaxHp;
	private int _playerStrength;
	private int _playerSearchActions;
	private int _carriedValue;
	private bool _runEnded;
	private bool _runFailed;
	private string _status = "Click a connected node to move.";

	public override void _Ready()
	{
		_rng.Randomize();
		BuildDemoState();
	}

	public override void _Process(double delta)
	{
		if (_encounter != null && !_runEnded)
		{
			_encounter.Timer += (float)delta;
			if (_encounter.Timer >= 0.55f)
			{
				_encounter.Timer = 0f;
				StepEncounter();
			}
		}

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
			ButtonDef button = _buttons[i];
			if (button.Rect.HasPoint(click))
			{
				HandleButton(button);
				return;
			}
		}

		if (_runEnded || _encounter != null)
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
		if (_encounter != null)
		{
			DrawEncounterOverlay();
		}
		if (_runEnded)
		{
			DrawEndOverlay();
		}
	}

	private void BuildDemoState()
	{
		_nodes.Clear();
		_aiSquads.Clear();
		_eventLog.Clear();

		_playerMaxHp = 24;
		_playerHp = 24;
		_playerStrength = 11;
		_playerSearchActions = 0;
		_carriedValue = 0;
		_turn = 0;
		_runEnded = false;
		_runFailed = false;
		_encounter = null;

		AddNode(0, "Entry Hall", NodeType.Room, new Vector2(120f, 360f), 0);
		AddNode(1, "Store Room", NodeType.Search, new Vector2(280f, 220f), 0);
		AddNode(2, "Guard Post", NodeType.Battle, new Vector2(290f, 500f), 4);
		AddNode(3, "Archive", NodeType.Search, new Vector2(470f, 160f), 0);
		AddNode(4, "Crossroad", NodeType.Room, new Vector2(470f, 360f), 2);
		AddNode(5, "Barracks", NodeType.Battle, new Vector2(470f, 560f), 5);
		AddNode(6, "Treasure Vault", NodeType.Search, new Vector2(660f, 220f), 3);
		AddNode(7, "South Gate", NodeType.Extract, new Vector2(650f, 500f), 0);

		LinkNodes(0, 1);
		LinkNodes(0, 2);
		LinkNodes(1, 3);
		LinkNodes(1, 4);
		LinkNodes(2, 4);
		LinkNodes(2, 5);
		LinkNodes(3, 4);
		LinkNodes(3, 6);
		LinkNodes(4, 5);
		LinkNodes(4, 6);
		LinkNodes(4, 7);
		LinkNodes(5, 7);
		LinkNodes(6, 7);

		_nodes[0].Visited = true;
		_nodes[0].ContainsPlayer = true;
		_playerNodeId = 0;

		PopulateRoomContainers();

		_aiSquads.Add(new AiSquad { Name = "Red Company", CurrentNodeId = 6, Strength = 9, Supplies = 3, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "Blue Company", CurrentNodeId = 5, Strength = 8, Supplies = 2, Intent = AiIntent.Idle });
		_aiSquads.Add(new AiSquad { Name = "Gold Company", CurrentNodeId = 3, Strength = 7, Supplies = 3, Intent = AiIntent.Idle });

		LogEvent("Run started. Other squads are already inside the map.");
		RefreshStatus();
	}

	private void AddNode(int id, string name, NodeType type, Vector2 position, int threat)
	{
		_nodes.Add(new MapNode
		{
			Id = id,
			Name = name,
			Type = type,
			Position = position,
			Threat = threat,
		});
	}

	private void LinkNodes(int a, int b)
	{
		_nodes[a].Links.Add(b);
		_nodes[b].Links.Add(a);
	}

	private void PopulateRoomContainers()
	{
		AddRoomContainer(1, "Supply Crate", 4, 0);
		AddRoomContainer(1, "Tool Locker", 3, 0);
		AddRoomContainer(3, "Document Chest", 4, 1);
		AddRoomContainer(4, "Abandoned Pack", 2, 0);
		AddRoomContainer(6, "Treasure Rack", 5, 2);
		AddRoomContainer(6, "Vault Drawer", 4, 1);
	}

	private void AddRoomContainer(int nodeId, string label, int hiddenCount, int visibleCount)
	{
		LootContainer container = new()
		{
			Label = label,
			Kind = ContainerKind.Room,
		};

		for (int i = 0; i < visibleCount; i++)
		{
			container.VisibleItems.Add(RollVisibleEquipment());
		}

		for (int i = 0; i < hiddenCount; i++)
		{
			container.HiddenItems.Add(RollLootItem());
		}

		_nodes[nodeId].Containers.Add(container);
	}

	private void TryMoveToNode(int nodeId)
	{
		MapNode current = _nodes[_playerNodeId];
		if (!current.Links.Contains(nodeId))
		{
			_status = "That node is not connected.";
			return;
		}

		current.ContainsPlayer = false;
		_playerNodeId = nodeId;
		MapNode target = _nodes[nodeId];
		target.ContainsPlayer = true;
		target.Visited = true;
		_playerSearchActions = 0;

		AdvanceTurn($"Moved to {target.Name}.");
		HandleArrival(target);
	}

	private void HandleArrival(MapNode node)
	{
		AiSquad occupyingSquad = GetSquadAtNode(node.Id);
		if (occupyingSquad != null && occupyingSquad.IsAlive)
		{
			StartEncounter(node, occupyingSquad.Name, occupyingSquad.Strength + 8, 1, occupyingSquad);
			return;
		}

		if (node.Threat > 0)
		{
			StartEncounter(node, $"{node.Name} defenders", node.Threat * 4 + 6, 1, null);
			return;
		}

		if (!node.SearchConsumed)
		{
			node.SearchConsumed = true;
			node.SearchTokensGranted = true;
			_playerSearchActions = 2;
			_status = $"Arrived at {node.Name}. Gained 2 free search actions.";
		}
		else
		{
			RefreshStatus();
		}
	}

	private void StartEncounter(MapNode node, string enemyName, int enemyHp, int turnCost, AiSquad squad)
	{
		_encounter = new EncounterState
		{
			Name = enemyName,
			PlayerHp = _playerHp + _playerStrength * 2,
			EnemyHp = enemyHp,
			TurnCost = turnCost,
			EnemySquad = squad,
			Node = node,
		};
		_encounter.Log.Add($"Encounter at {node.Name}.");
		_encounter.Log.Add($"Enemy: {enemyName}.");
		_status = $"Battle started at {node.Name}.";
	}

	private void StepEncounter()
	{
		if (_encounter == null)
		{
			return;
		}

		int playerDamage = _rng.RandiRange(3, 6) + Mathf.Max(0, _playerStrength / 3);
		int enemyDamage = _rng.RandiRange(2, 5) + (_encounter.EnemySquad != null ? 1 : 0);
		_encounter.EnemyHp = Mathf.Max(0, _encounter.EnemyHp - playerDamage);
		_encounter.PlayerHp = Mathf.Max(0, _encounter.PlayerHp - enemyDamage);
		_encounter.Log.Add($"You deal {playerDamage}. Enemy deals {enemyDamage}.");
		if (_encounter.Log.Count > 7)
		{
			_encounter.Log.RemoveAt(0);
		}

		if (_encounter.PlayerHp <= 0 || _encounter.EnemyHp <= 0)
		{
			ResolveEncounter();
		}
	}

	private void ResolveEncounter()
	{
		if (_encounter == null)
		{
			return;
		}

		MapNode node = _encounter.Node;
		AiSquad squad = _encounter.EnemySquad;
		bool playerWon = _encounter.PlayerHp > 0;
		AdvanceTurn(playerWon ? $"Won battle at {node.Name}." : $"Fell in battle at {node.Name}.", _encounter.TurnCost, false);

		if (!playerWon)
		{
			_playerHp = 0;
			_runEnded = true;
			_runFailed = true;
			_status = "The run is over.";
			LogEvent("Your squad was wiped out.");
			_encounter = null;
			return;
		}

		int remainingHp = Mathf.Clamp((_encounter.PlayerHp + 1) / 3, 4, _playerMaxHp);
		_playerHp = remainingHp;
		_playerStrength = Mathf.Max(5, _playerStrength - _rng.RandiRange(0, 2));
		node.Threat = 0;
		node.Cleared = true;
		GenerateBattleLoot(node, squad);
		_playerSearchActions = 0;

		if (squad != null)
		{
			squad.Intent = AiIntent.Defeated;
			squad.Strength = 0;
			LogEvent($"{squad.Name} was destroyed by the player.");
		}
		else
		{
			LogEvent($"The defenders at {node.Name} were defeated.");
		}

		_status = $"Battle won at {node.Name}. Search the remains if you want loot.";
		_encounter = null;
	}

	private void GenerateBattleLoot(MapNode node, AiSquad squad)
	{
		LootContainer pile = new()
		{
			Label = "Corpse Pile",
			Kind = ContainerKind.CorpsePile,
		};

		int pileCount = _rng.RandiRange(3, 5);
		for (int i = 0; i < pileCount; i++)
		{
			pile.HiddenItems.Add(squad != null && squad.Loot.Count > 0 ? TakeRandomSquadLoot(squad) : RollLootItem());
		}

		node.Containers.Add(pile);

		if (squad != null)
		{
			LootContainer leader = new()
			{
				Label = $"{squad.Name} Captain",
				Kind = ContainerKind.EliteCorpse,
			};
			leader.VisibleItems.Add("Steel Saber");
			leader.VisibleItems.Add("Captain Mail");
			leader.HiddenItems.Add("Bandage Kit");
			if (squad.Loot.Count > 0)
			{
				leader.HiddenItems.Add(TakeRandomSquadLoot(squad));
			}
			leader.HiddenItems.Add("Seal Token");
			node.Containers.Add(leader);
		}
		else if (node.Type == NodeType.Battle)
		{
			LootContainer elite = new()
			{
				Label = "Elite Guard",
				Kind = ContainerKind.EliteCorpse,
			};
			elite.VisibleItems.Add("Guard Spear");
			elite.HiddenItems.Add("Ration Pack");
			elite.HiddenItems.Add(RollLootItem());
			node.Containers.Add(elite);
		}
	}

	private string TakeRandomSquadLoot(AiSquad squad)
	{
		int index = _rng.RandiRange(0, squad.Loot.Count - 1);
		string item = squad.Loot[index];
		squad.Loot.RemoveAt(index);
		return item;
	}

	private void AdvanceTurn(string reason, int amount = 1, bool refreshPlayerNode = true)
	{
		for (int i = 0; i < amount; i++)
		{
			_turn++;
			SimulateAiTurn();
		}

		LogEvent($"Turn {_turn}: {reason}");
		if (refreshPlayerNode && !_runEnded)
		{
			RefreshStatus();
		}
	}

	private void SimulateAiTurn()
	{
		ResolveAiDuels();

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
				if (squad.CurrentNodeId == extractId)
				{
					squad.Intent = AiIntent.Extracted;
					LogEvent($"{squad.Name} extracted with {squad.Loot.Count} loot items.");
					continue;
				}

				MoveAiToward(squad, extractId, AiIntent.Extracting);
				continue;
			}

			MapNode node = _nodes[squad.CurrentNodeId];
			if (node.Threat > 0)
			{
				squad.Intent = AiIntent.Clearing;
				squad.BusyTurns = _rng.RandiRange(1, 2);
				LogEvent($"{squad.Name} started clearing {node.Name}.");
				continue;
			}

			if (CanAiLootNode(node))
			{
				squad.Intent = AiIntent.Looting;
				squad.BusyTurns = 1;
				LogEvent($"{squad.Name} started looting {node.Name}.");
				continue;
			}

			int targetNodeId = PickAiTargetNode(squad);
			MoveAiToward(squad, targetNodeId, AiIntent.Moving);
		}

		ResolveAiDuels();
	}

	private void ResolveAiBusyAction(AiSquad squad)
	{
		if (!squad.IsAlive)
		{
			return;
		}

		MapNode node = _nodes[squad.CurrentNodeId];
		switch (squad.Intent)
		{
			case AiIntent.Clearing:
			{
				int loss = _rng.RandiRange(0, 2);
				squad.Strength = Mathf.Max(1, squad.Strength - loss);
				node.Threat = 0;
				node.Cleared = true;
				LogEvent($"{squad.Name} cleared {node.Name} and lost {loss} strength.");
				break;
			}
			case AiIntent.Looting:
			{
				if (TryAiLootNode(squad, node))
				{
					LogEvent($"{squad.Name} looted {node.Name}.");
				}
				break;
			}
			case AiIntent.FightingSquad:
			{
				ResolveAiVsAiBattle(squad);
				break;
			}
		}

		squad.Intent = AiIntent.Idle;
	}

	private void ResolveAiDuels()
	{
		for (int i = 0; i < _aiSquads.Count; i++)
		{
			AiSquad a = _aiSquads[i];
			if (!a.IsAlive || a.BusyTurns > 0)
			{
				continue;
			}

			for (int j = i + 1; j < _aiSquads.Count; j++)
			{
				AiSquad b = _aiSquads[j];
				if (!b.IsAlive || b.BusyTurns > 0)
				{
					continue;
				}

				if (a.CurrentNodeId != b.CurrentNodeId || a.CurrentNodeId == _playerNodeId)
				{
					continue;
				}

				a.Intent = AiIntent.FightingSquad;
				b.Intent = AiIntent.FightingSquad;
				a.BusyTurns = _rng.RandiRange(3, 5);
				b.BusyTurns = a.BusyTurns;
				a.RivalSquadId = j;
				b.RivalSquadId = i;
				LogEvent($"{a.Name} and {b.Name} engaged at {_nodes[a.CurrentNodeId].Name}.");
			}
		}
	}

	private void ResolveAiVsAiBattle(AiSquad squad)
	{
		if (squad.RivalSquadId < 0 || squad.RivalSquadId >= _aiSquads.Count)
		{
			return;
		}

		AiSquad rival = _aiSquads[squad.RivalSquadId];
		if (!rival.IsAlive || rival.Intent != AiIntent.FightingSquad)
		{
			squad.RivalSquadId = -1;
			return;
		}

		if (rival.RivalSquadId != _aiSquads.IndexOf(squad))
		{
			return;
		}

		int squadPower = squad.Strength + _rng.RandiRange(0, 4);
		int rivalPower = rival.Strength + _rng.RandiRange(0, 4);
		bool squadWins = squadPower >= rivalPower;
		AiSquad winner = squadWins ? squad : rival;
		AiSquad loser = squadWins ? rival : squad;
		int winnerLoss = _rng.RandiRange(1, 3);

		winner.Strength = Mathf.Max(1, winner.Strength - winnerLoss);
		loser.Strength = 0;
		loser.Intent = AiIntent.Defeated;
		loser.BusyTurns = 0;
		winner.Intent = AiIntent.Idle;
		winner.BusyTurns = 0;
		winner.RivalSquadId = -1;
		loser.RivalSquadId = -1;

		MapNode node = _nodes[winner.CurrentNodeId];
		LootContainer pile = new()
		{
			Label = $"{loser.Name} Remains",
			Kind = ContainerKind.CorpsePile,
		};
		pile.HiddenItems.Add("Broken Badge");
		pile.HiddenItems.Add("Field Ration");
		pile.HiddenItems.Add(RollLootItem());
		node.Containers.Add(pile);
		LogEvent($"{winner.Name} defeated {loser.Name} at {node.Name}.");
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
			if (container.VisibleItems.Count > 0)
			{
				squad.Loot.Add(container.VisibleItems[0]);
				container.VisibleItems.RemoveAt(0);
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}

			if (container.HiddenRemaining > 0)
			{
				squad.Loot.Add(container.HiddenItems[container.RevealIndex]);
				container.RevealIndex++;
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}
		}

		node.SearchedOut = true;
		return false;
	}

	private int PickAiTargetNode(AiSquad squad)
	{
		int bestNodeId = squad.CurrentNodeId;
		float bestScore = float.MinValue;
		foreach (MapNode node in _nodes)
		{
			if (node.Id == squad.CurrentNodeId)
			{
				continue;
			}

			float score = 0f;
			score += node.Type == NodeType.Search ? 7f : 0f;
			score += node.Type == NodeType.Battle ? 4f : 0f;
			score += node.Threat > 0 ? 3f : 0f;
			score += CountNodeLoot(node) * 1.5f;
			score -= _nodes[squad.CurrentNodeId].Position.DistanceTo(node.Position) * 0.01f;
			if (node.Id == _playerNodeId)
			{
				score += 2f;
			}

			if (score > bestScore)
			{
				bestScore = score;
				bestNodeId = node.Id;
			}
		}

		return bestNodeId;
	}

	private void MoveAiToward(AiSquad squad, int targetNodeId, AiIntent intent)
	{
		int nextNodeId = FindNextStepToward(squad.CurrentNodeId, targetNodeId);
		if (nextNodeId == squad.CurrentNodeId)
		{
			return;
		}

		squad.CurrentNodeId = nextNodeId;
		squad.Intent = intent;
		squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
		LogEvent($"{squad.Name} moved to {_nodes[nextNodeId].Name}.");
	}

	private int FindNextStepToward(int fromNodeId, int targetNodeId)
	{
		if (fromNodeId == targetNodeId)
		{
			return fromNodeId;
		}

		MapNode from = _nodes[fromNodeId];
		int best = fromNodeId;
		float bestDistance = float.MaxValue;
		foreach (int link in from.Links)
		{
			float distance = _nodes[link].Position.DistanceTo(_nodes[targetNodeId].Position);
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
			if (squad.IsAlive && squad.CurrentNodeId == nodeId)
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
				BuildDemoState();
			}
			return;
		}

		switch (button.Action)
		{
			case "search":
				TrySearchContainer(button.Index);
				break;
			case "take":
				TakeVisibleItem(button.Index);
				break;
			case "buy_search":
				BuySearchActions();
				break;
			case "extract":
				ExtractNow();
				break;
		}
	}

	private void TrySearchContainer(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanPlayerSearchNode(node))
		{
			_status = "This room is not safe enough to search.";
			return;
		}

		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		if (container.HiddenRemaining <= 0)
		{
			_status = "Nothing hidden remains in that container.";
			return;
		}

		if (_playerSearchActions <= 0)
		{
			_status = "No search actions left. Buy more with 1 turn.";
			return;
		}

		_playerSearchActions--;
		string item = container.HiddenItems[container.RevealIndex];
		container.RevealIndex++;
		AddLootToPlayer(item);
		LogEvent($"Searched {container.Label} and found {item}.");
		RefreshStatus();
	}

	private void TakeVisibleItem(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanPlayerSearchNode(node))
		{
			_status = "The room is still dangerous.";
			return;
		}

		int containerIndex = encodedIndex / 100;
		int itemIndex = encodedIndex % 100;
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		if (itemIndex < 0 || itemIndex >= container.VisibleItems.Count)
		{
			return;
		}

		string item = container.VisibleItems[itemIndex];
		container.VisibleItems.RemoveAt(itemIndex);
		AddLootToPlayer(item);
		LogEvent($"Took visible item {item} from {container.Label}.");
		RefreshStatus();
	}

	private void BuySearchActions()
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanPlayerSearchNode(node))
		{
			_status = "You cannot settle down to search here.";
			return;
		}

		AdvanceTurn($"Spent time searching {node.Name}.", 1);
		_playerSearchActions += 4;
		_status = $"Bought 4 search actions at {node.Name}.";
	}

	private void ExtractNow()
	{
		MapNode node = _nodes[_playerNodeId];
		if (node.Type != NodeType.Extract)
		{
			_status = "You are not at an extract node.";
			return;
		}

		_runEnded = true;
		_runFailed = false;
		_status = "Extraction successful.";
		LogEvent("The player extracted successfully.");
	}

	private bool CanPlayerSearchNode(MapNode node)
	{
		return node.Threat <= 0 && GetSquadAtNode(node.Id) == null && _encounter == null;
	}

	private void AddLootToPlayer(string item)
	{
		_carriedValue += GetItemValue(item);
	}

	private int GetItemValue(string item)
	{
		if (item.Contains("Relic") || item.Contains("Seal") || item.Contains("Gem"))
		{
			return 18;
		}

		if (item.Contains("Mail") || item.Contains("Saber") || item.Contains("Spear"))
		{
			return 12;
		}

		if (item.Contains("Kit") || item.Contains("Ration"))
		{
			return 7;
		}

		return 5;
	}

	private string RollLootItem()
	{
		string[] items =
		[
			"Old Relic",
			"Silver Gem",
			"Ration Pack",
			"Herb Kit",
			"Seal Token",
			"Trade Ledger",
			"Rust Key",
			"Lantern Oil",
			"Fine Cloth",
		];
		return items[_rng.RandiRange(0, items.Length - 1)];
	}

	private string RollVisibleEquipment()
	{
		string[] items =
		[
			"Merc Sword",
			"Leather Vest",
			"Hunter Bow",
			"Tower Shield",
			"Steel Helm",
		];
		return items[_rng.RandiRange(0, items.Length - 1)];
	}

	private int CountNodeLoot(MapNode node)
	{
		int count = 0;
		foreach (LootContainer container in node.Containers)
		{
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
			_status = $"Enemy squad {enemy.Name} is here.";
			return;
		}

		if (node.Threat > 0)
		{
			_status = $"{node.Name} still has defenders.";
			return;
		}

		if (node.Type == NodeType.Extract)
		{
			_status = "This is an extraction point.";
			return;
		}

		_status = $"Safe at {node.Name}. Search actions: {_playerSearchActions}.";
	}

	private void LogEvent(string message)
	{
		_eventLog.Add(message);
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
				if (link < node.Id)
				{
					continue;
				}

				DrawLine(node.Position, _nodes[link].Position, new Color(0.28f, 0.32f, 0.36f), 3f);
			}
		}

		foreach (MapNode node in _nodes)
		{
			Color color = GetNodeColor(node);
			DrawCircle(node.Position, 22f, color);
			DrawArc(node.Position, 28f, 0f, Mathf.Tau, 32, new Color(color.R, color.G, color.B, 0.45f), 2f);
			DrawString(ThemeDB.FallbackFont, node.Position + new Vector2(-42f, -32f), node.Name, HorizontalAlignment.Left, -1f, 14, Colors.White);

			if (node.ContainsPlayer)
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
		if (node.Type == NodeType.Extract)
		{
			return new Color(0.26f, 0.72f, 0.42f);
		}

		if (node.Threat > 0)
		{
			return new Color(0.72f, 0.3f, 0.28f);
		}

		if (CountNodeLoot(node) > 0)
		{
			return new Color(0.75f, 0.62f, 0.24f);
		}

		return node.Visited ? new Color(0.31f, 0.45f, 0.62f) : new Color(0.2f, 0.23f, 0.28f);
	}

	private void DrawSidePanel()
	{
		DrawRect(_sideRect, new Color(0.05f, 0.05f, 0.06f, 0.96f), true);
		DrawRect(_sideRect, Colors.White, false, 2f);

		float x = _sideRect.Position.X + 18f;
		float y = _sideRect.Position.Y + 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "Node Raid Demo", HorizontalAlignment.Left, -1f, 20, Colors.White);
		y += 28f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Turn {_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.76f, 0.84f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"HP {_playerHp}/{_playerMaxHp}   Strength {_playerStrength}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 22f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Search {_playerSearchActions}   Value {_carriedValue}", HorizontalAlignment.Left, -1f, 15, Colors.White);
		y += 26f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, 320f, 14, new Color(0.86f, 0.9f, 0.95f));
		y += 54f;

		MapNode node = _nodes[_playerNodeId];
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Current Node: {node.Name}", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Type: {node.Type}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 18f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Threat: {node.Threat}   Loot: {CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, 13, new Color(0.75f, 0.78f, 0.82f));
		y += 28f;

		if (node.Type == NodeType.Extract && _encounter == null && !_runEnded)
		{
			Rect2 extractButton = new(new Vector2(x, y), new Vector2(140f, 30f));
			DrawButton(extractButton, "Extract", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(extractButton, "extract"));
			y += 42f;
		}

		if (CanPlayerSearchNode(node) && CountNodeLoot(node) > 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "Containers", HorizontalAlignment.Left, -1f, 16, Colors.White);
			y += 22f;
			for (int i = 0; i < node.Containers.Count; i++)
			{
				LootContainer container = node.Containers[i];
				if (container.IsEmpty)
				{
					continue;
				}

				DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"{container.Label} [{container.Kind}]", HorizontalAlignment.Left, 320f, 13, new Color(1f, 0.93f, 0.7f));
				y += 18f;
				if (container.VisibleItems.Count > 0)
				{
					for (int itemIndex = 0; itemIndex < container.VisibleItems.Count; itemIndex++)
					{
						string item = container.VisibleItems[itemIndex];
						Rect2 takeButton = new(new Vector2(x, y), new Vector2(116f, 24f));
						DrawButton(takeButton, $"Take {item}", new Color(0.26f, 0.42f, 0.58f));
						_buttons.Add(new ButtonDef(takeButton, "take", i * 100 + itemIndex));
						y += 30f;
					}
				}

				if (container.HiddenRemaining > 0)
				{
					Rect2 searchButton = new(new Vector2(x, y), new Vector2(200f, 24f));
					DrawButton(searchButton, $"Inspect hidden item ({container.HiddenRemaining})", new Color(0.54f, 0.42f, 0.18f));
					_buttons.Add(new ButtonDef(searchButton, "search", i));
					y += 30f;
				}

				y += 4f;
			}

			if (_playerSearchActions <= 0)
			{
				Rect2 buyButton = new(new Vector2(x, y), new Vector2(220f, 28f));
				DrawButton(buyButton, "Spend 1 turn for 4 searches", new Color(0.46f, 0.25f, 0.19f));
				_buttons.Add(new ButtonDef(buyButton, "buy_search"));
				y += 38f;
			}
		}

		float logY = _sideRect.End.Y - 190f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), "World Log", HorizontalAlignment.Left, -1f, 16, Colors.White);
		logY += 20f;
		for (int i = Mathf.Max(0, _eventLog.Count - 8); i < _eventLog.Count; i++)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), _eventLog[i], HorizontalAlignment.Left, 320f, 12, new Color(0.8f, 0.84f, 0.9f));
			logY += 18f;
		}
	}

	private void DrawButton(Rect2 rect, string label, Color color)
	{
		DrawRect(rect, color, true);
		DrawRect(rect, Colors.White, false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(8f, 17f), label, HorizontalAlignment.Left, rect.Size.X - 12f, 12, Colors.White);
	}

	private void DrawEncounterOverlay()
	{
		Rect2 panel = new(new Vector2(200f, 120f), new Vector2(800f, 420f));
		DrawRect(panel, new Color(0.02f, 0.02f, 0.03f, 0.95f), true);
		DrawRect(panel, Colors.White, false, 2f);

		float x = panel.Position.X + 24f;
		float y = panel.Position.Y + 34f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Battle: {_encounter.Name}", HorizontalAlignment.Left, -1f, 22, Colors.White);
		y += 34f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Player battle HP: {_encounter.PlayerHp}", HorizontalAlignment.Left, -1f, 16, new Color(0.65f, 0.95f, 0.72f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Enemy battle HP: {_encounter.EnemyHp}", HorizontalAlignment.Left, -1f, 16, new Color(0.95f, 0.56f, 0.56f));
		y += 34f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "Auto-resolving combat...", HorizontalAlignment.Left, -1f, 15, new Color(0.82f, 0.86f, 0.92f));
		y += 28f;

		foreach (string line in _encounter.Log)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), line, HorizontalAlignment.Left, 730f, 14, new Color(0.84f, 0.88f, 0.95f));
			y += 24f;
		}
	}

	private void DrawEndOverlay()
	{
		Rect2 panel = new(new Vector2(250f, 170f), new Vector2(720f, 330f));
		DrawRect(panel, new Color(0.01f, 0.01f, 0.02f, 0.96f), true);
		DrawRect(panel, Colors.White, false, 2f);

		float x = panel.Position.X + 26f;
		float y = panel.Position.Y + 38f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _runFailed ? "Run Failed" : "Extraction Complete", HorizontalAlignment.Left, -1f, 24, Colors.White);
		y += 40f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Turns survived: {_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.82f, 0.87f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"Loot value carried out: {_carriedValue}", HorizontalAlignment.Left, -1f, 16, new Color(0.95f, 0.86f, 0.48f));
		y += 36f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "AI Squad Summary", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;

		foreach (AiSquad squad in _aiSquads)
		{
			string line = $"{squad.Name}: {squad.Intent}, strength {Mathf.Max(0, squad.Strength)}, loot {squad.Loot.Count}";
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), line, HorizontalAlignment.Left, 620f, 14, new Color(0.82f, 0.86f, 0.92f));
			y += 22f;
		}

		Rect2 restartButton = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(140f, 30f));
		DrawButton(restartButton, "Restart Run", new Color(0.26f, 0.45f, 0.62f));
		_buttons.Add(new ButtonDef(restartButton, "restart"));
	}
}
