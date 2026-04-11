using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RaidMapDemo : Node2D
{
	private const int RecruitCost = 30;
	private const int MapTemplateCount = 2;
	private const int DifficultyCount = 3;
	private const bool EnableCombatFxDebugOpening = false;
	private const int SfxSampleRate = 22050;
	private const int TeamBackpackMaxWidth = 12;
	private const int TeamBackpackMaxRows = 12;
	private const int StashGridWidth = 12;
	private const int StashGridHeight = 10;
	private static readonly Rect2 DesignMapRect = new(new Vector2(30f, 30f), new Vector2(760f, 660f));

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
		public readonly List<RoomCorpseMarker> Corpses = new();
	}

	private sealed class RoomCorpseMarker
	{
		public bool IsPlayerSide;
		public bool IsElite;
		public bool IsHero;
		public bool IsAiSquad;
		public Vector2 Position;
	}

	private sealed class LootContainer
	{
		public string Label = "";
		public ContainerKind Kind;
		public Vector2 Position;
		public Color Tint = new(0.76f, 0.74f, 0.7f, 1f);
		public float AutoOpenRange = 54f;
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

	private sealed class RoomResourceOrb
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public int MoneyAmount;
		public Color Tint = new(1f, 0.84f, 0.34f, 1f);
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
		public bool AcquiredInRun;
		public bool Revealed;
		public bool Taken;
		public float SearchTime;
		public float SearchProgress;
	}

	private sealed class BackpackItem
	{
		public string Label = "";
		public ItemRarity Rarity;
		public Vector2I Size;
		public Vector2I Cell;
		public bool AcquiredInRun;
	}

	private sealed class BackpackCapacityBlock
	{
		public string SourceLabel = "";
		public Vector2I Size;
		public Vector2I Cell;
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

	private enum RoomCombatState
	{
		Idle,
		Advance,
		AttackCommit,
		Retreat,
		Regroup,
	}

	private sealed class RoomUnit
	{
		public bool IsPlayerSide;
		public bool IsHero;
		public bool IsElite;
		public bool IsAiSquad;
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
		public RoomCombatState CombatState;
		public float CombatStateTimer;
		public Vector2 TacticalAnchor;
		public float Stamina;
		public float MaxStamina;
		public bool IsSprinting;
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
	private readonly List<BackpackItem> _runBackpack = new();
	private readonly List<BackpackItem> _overflowBackpackItems = new();
	private readonly List<BackpackItem> _stash = new();
	private readonly List<BackpackItem> _hideoutLoadout = new();
	private readonly List<ShopEntry> _shopStock = new();
	private readonly List<SoldierRecord> _soldierRoster = new();
	private readonly List<SoldierRecord> _runSoldiers = new();
	private readonly List<RoomUnit> _roomUnits = new();
	private readonly List<RoomProjectileEffect> _roomProjectileEffects = new();
	private readonly List<RoomMeleeArcEffect> _roomMeleeArcEffects = new();
	private readonly List<RoomResourceOrb> _roomResourceOrbs = new();
	private readonly List<AudioStreamPlayer> _sfxPlayers = new();
	private readonly RandomNumberGenerator _rng = new();

	private Rect2 _mapRect = new(new Vector2(30f, 30f), new Vector2(760f, 660f));
	private Rect2 _sideRect = new(new Vector2(810f, 30f), new Vector2(360f, 660f));
	private float _uiScale = 1f;

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
	private int _runMoneyLooted;
	private int _nextSoldierId = 1;
	private bool _runEnded;
	private bool _runFailed;
	private bool _inHideout = true;
	private bool _showSettlementTransfer;
	private bool _autoSearchEnabled;
	private bool _skipSearchConfirm;
	private bool _showSearchConfirm;
	private bool _confirmSkipChecked;
	private int _selectedContainerIndex = -1;
	private int _selectedStashIndex = -1;
	private int _selectedShopIndex = -1;
	private int _selectedLoadoutIndex = -1;
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
	private Vector2 _pendingExitDirection = Vector2.Right;
	private bool _roomDirty;
	private string _status = "点击相邻节点移动。";
	private BackpackItem _draggedBackpackItem;
	private bool _hasDraggedBackpackItem;
	private Vector2I _draggedBackpackOriginalCell;
	private Vector2 _draggedBackpackGrabOffset;
	private BackpackItem _draggedHideoutItem;
	private bool _hasDraggedHideoutItem;
	private Vector2I _draggedHideoutOriginalCell;
	private Vector2 _draggedHideoutGrabOffset;
	private bool _draggedHideoutFromLoadout;
	private bool _pendingHideoutDrag;
	private bool _pendingHideoutDragFromLoadout;
	private int _pendingHideoutDragIndex = -1;
	private Vector2 _pendingHideoutGrabOffset;
	private int _selectedSettlementIndex = -1;
	private int _sfxCursor;
	private AudioStreamWav _sfxMeleeLight;
	private AudioStreamWav _sfxMeleeHeavy;
	private AudioStreamWav _sfxRangedLight;
	private AudioStreamWav _sfxRangedHeavy;
	private AudioStreamWav _sfxHitLight;
	private AudioStreamWav _sfxHitHeavy;
	private AudioStreamWav _sfxDeath;

	public override void _Ready()
	{
		_rng.Randomize();
		InitializeSfx();
		UpdateLayoutRects();
		InitHideout();
	}

	private void InitializeSfx()
	{
		_sfxMeleeLight = CreateProceduralSfx(220f, 120f, 0.075f, 0.42f, 0.08f, 0.18f);
		_sfxMeleeHeavy = CreateProceduralSfx(170f, 82f, 0.11f, 0.56f, 0.12f, 0.28f);
		_sfxRangedLight = CreateProceduralSfx(760f, 520f, 0.04f, 0.26f, 0.02f, 0.04f);
		_sfxRangedHeavy = CreateProceduralSfx(620f, 360f, 0.055f, 0.32f, 0.03f, 0.08f);
		_sfxHitLight = CreateProceduralSfx(150f, 72f, 0.06f, 0.36f, 0.18f, 0.16f);
		_sfxHitHeavy = CreateProceduralSfx(120f, 48f, 0.09f, 0.5f, 0.24f, 0.22f);
		_sfxDeath = CreateProceduralSfx(200f, 44f, 0.18f, 0.38f, 0.14f, 0.12f);

		for (int i = 0; i < 8; i++)
		{
			AudioStreamPlayer player = new()
			{
				Bus = "Master",
				Autoplay = false,
				VolumeDb = -9f,
			};
			AddChild(player);
			_sfxPlayers.Add(player);
		}
	}

	private AudioStreamWav CreateProceduralSfx(float startFrequency, float endFrequency, float duration, float peak, float noiseMix, float squareMix)
	{
		int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SfxSampleRate));
		byte[] pcm = new byte[sampleCount * 2];
		float phase = 0f;
		uint noiseState = (uint)(startFrequency * 31f + endFrequency * 17f + duration * 1000f + 1f);
		for (int i = 0; i < sampleCount; i++)
		{
			float t = sampleCount <= 1 ? 1f : i / (float)(sampleCount - 1);
			float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
			phase += Mathf.Tau * frequency / SfxSampleRate;
			noiseState = noiseState * 1664525u + 1013904223u;
			float noise = ((noiseState >> 9) & 1023) / 511.5f - 1f;
			float sine = Mathf.Sin(phase);
			float square = sine >= 0f ? 1f : -1f;
			float bodyMix = Mathf.Max(0f, 1f - noiseMix - squareMix);
			float envelope = t < 0.08f
				? t / 0.08f
				: Mathf.Pow(Mathf.Max(0f, 1f - t), 1.6f);
			float sample = (sine * bodyMix + square * squareMix + noise * noiseMix) * envelope * peak;
			short pcmValue = (short)Mathf.Clamp(Mathf.RoundToInt(sample * short.MaxValue), short.MinValue, short.MaxValue);
			pcm[i * 2] = (byte)(pcmValue & 0xff);
			pcm[i * 2 + 1] = (byte)((pcmValue >> 8) & 0xff);
		}

		return new AudioStreamWav
		{
			Data = pcm,
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = SfxSampleRate,
			Stereo = false,
		};
	}

	private void PlayAttackSfx(bool ranged, bool heavy)
	{
		PlaySfx(ranged
			? (heavy ? _sfxRangedHeavy : _sfxRangedLight)
			: (heavy ? _sfxMeleeHeavy : _sfxMeleeLight), heavy ? -7f : -9f);
	}

	private void PlayHitSfx(bool heavy, bool killingBlow)
	{
		PlaySfx(killingBlow ? _sfxDeath : (heavy ? _sfxHitHeavy : _sfxHitLight), killingBlow ? -8f : (heavy ? -7f : -9f));
	}

	private void PlaySfx(AudioStream stream, float volumeDb)
	{
		if (stream == null || _sfxPlayers.Count == 0)
		{
			return;
		}

		AudioStreamPlayer player = _sfxPlayers[_sfxCursor];
		_sfxCursor = (_sfxCursor + 1) % _sfxPlayers.Count;
		player.Stop();
		player.Stream = stream;
		player.VolumeDb = volumeDb;
		player.PitchScale = 1f + _rng.RandfRange(-0.04f, 0.04f);
		player.Play();
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
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.M)
			{
				ToggleMapOverlay();
				return;
			}

			if (_hasDraggedBackpackItem && keyEvent.Keycode == Key.R)
			{
				_draggedBackpackItem.Size = new Vector2I(_draggedBackpackItem.Size.Y, _draggedBackpackItem.Size.X);
				return;
			}

			if (_hasDraggedHideoutItem && keyEvent.Keycode == Key.R)
			{
				_draggedHideoutItem.Size = new Vector2I(_draggedHideoutItem.Size.Y, _draggedHideoutItem.Size.X);
				return;
			}

			if (_hasDraggedBackpackItem && keyEvent.Keycode == Key.G)
			{
				DropDraggedBackpackItemToGround();
				return;
			}
		}

		if (@event is InputEventMouseMotion mouseMotion)
		{
			if ((_inHideout || (_runEnded && _showSettlementTransfer))
				&& _pendingHideoutDrag
				&& (((int)mouseMotion.ButtonMask & (int)MouseButtonMask.Left) != 0))
			{
				TryBeginPendingHideoutDrag();
			}

			return;
		}

		if (@event is not InputEventMouseButton mouse || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		Vector2 click = mouse.Position;
		if ((_inHideout || (_runEnded && _showSettlementTransfer)))
		{
			if (mouse.Pressed)
			{
				if (HandleHideoutStoragePress(click))
				{
					return;
				}
			}
			else
			{
				if (HandleHideoutStorageRelease(click))
				{
					return;
				}
			}
		}

		if (!mouse.Pressed)
		{
			return;
		}

		if (!_inHideout && !_runEnded && _hasDraggedBackpackItem && HandleOpenContainerPopupClick(click))
		{
			return;
		}

		for (int i = _buttons.Count - 1; i >= 0; i--)
		{
			if (_buttons[i].Rect.HasPoint(click))
			{
				HandleButton(_buttons[i]);
				return;
			}
		}

		if (_inHideout || _runEnded)
		{
			return;
		}

		if (HandleBackpackPreviewClick(click))
		{
			return;
		}

		if (HandleOpenContainerPopupClick(click))
		{
			return;
		}

		if (_showMapOverlay)
		{
			foreach (MapNode node in _nodes)
			{
				if (MapToScreen(node.Position).DistanceTo(click) <= 24f * GetMapUnitScale())
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
			DrawDraggedHideoutOverlay();
			return;
		}
		if (_runEnded && _showSettlementTransfer)
		{
			DrawSettlementTransfer();
			DrawDraggedHideoutOverlay();
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
		DrawDraggedBackpackOverlay();
		if (_runEnded)
		{
			DrawEndOverlay();
		}
	}

	private void UpdateLayoutRects()
	{
		Vector2 viewport = GetViewportRect().Size;
		_uiScale = Mathf.Clamp(Mathf.Min(viewport.X / 1360f, viewport.Y / 760f), 1f, 1.22f);
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

	private float Ui(float value) => value * _uiScale;

	private int UiFont(int size) => Mathf.RoundToInt(size * _uiScale);

	private void ResetRun()
	{
		_inHideout = false;
		_nodes.Clear();
		_aiSquads.Clear();
		_eventLog.Clear();
		_runBackpack.Clear();
		_overflowBackpackItems.Clear();
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		_pendingHideoutGrabOffset = Vector2.Zero;

		_playerMaxHp = 24;
		_playerHp = 24;
		_playerStrength = 11;
		_timeSlotProgress = 0;
		_lootValue = 0;
		_runMoneyLooted = 0;
		_turn = 0;
		_runEnded = false;
		_runFailed = false;
		_showSettlementTransfer = false;
		_autoSearchEnabled = true;
		_showMapOverlay = false;
		_plannedExitNodeId = -1;
		_selectedContainerIndex = -1;
		_selectedStashIndex = -1;
		_selectedShopIndex = -1;
		_selectedLoadoutIndex = -1;
		_selectedSettlementIndex = -1;
		_encounter = null;
		_roomUnits.Clear();
		_roomProjectileEffects.Clear();
		_roomMeleeArcEffects.Clear();
		_roomResourceOrbs.Clear();
		_heroHasMoveTarget = false;
		_pendingExitNodeId = -1;
		_roomDirty = true;
		_runSoldiers.Clear();
		foreach (SoldierRecord soldier in _soldierRoster)
		{
			_runSoldiers.Add(new SoldierRecord { Name = soldier.Name });
		}

		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			_runBackpack.Add(CloneBackpackItem(_hideoutLoadout[i]));
		}
		_hideoutLoadout.Clear();

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
			EnterNodeRoom(_playerNodeId, Vector2.Left, false);
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
		EnterNodeRoom(_playerNodeId, Vector2.Left, false);
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
		if (!_inHideout && _runEnded)
		{
			CommitRunSoldierRoster();
		}

		_inHideout = true;
		_runEnded = false;
		_runFailed = false;
		_showSettlementTransfer = false;
		_selectedContainerIndex = -1;
		_selectedStashIndex = -1;
		_selectedShopIndex = -1;
		_selectedLoadoutIndex = -1;
		_selectedSettlementIndex = -1;
		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		_pendingHideoutGrabOffset = Vector2.Zero;
		_battleSim = null;
		_eventLog.Clear();
		if (_shopStock.Count == 0)
		{
			SeedShop();
		}
		if (_money == 0 && _stash.Count == 0)
		{
			_money = 3000;
			TryAddToStash("旧军刀");
			TryAddToStash("草药包");
		}
		if (_soldierRoster.Count == 0 && _nextSoldierId == 1)
		{
			RecruitSoldierInternal();
			RecruitSoldierInternal();
		}
	}

	private void CommitRunSoldierRoster()
	{
		_soldierRoster.Clear();
		for (int i = 0; i < _runSoldiers.Count; i++)
		{
			_soldierRoster.Add(new SoldierRecord { Name = _runSoldiers[i].Name });
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
		LootContainer container = new()
		{
			Label = label,
			Kind = ContainerKind.Room,
			Position = GetPresetContainerPosition(nodeId, _nodes[nodeId].Containers.Count),
			Tint = new Color(0.76f, 0.68f, 0.34f, 1f),
			AutoOpenRange = 56f,
		};
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

	private Vector2 GetPresetContainerPosition(int nodeId, int slotIndex)
	{
		Rect2 rect = GetRoomArenaRect();
		Vector2[] anchors =
		[
			new Vector2(rect.Position.X + 84f, rect.Position.Y + 92f),
			new Vector2(rect.End.X - 96f, rect.Position.Y + 96f),
			new Vector2(rect.Position.X + 102f, rect.End.Y - 92f),
			new Vector2(rect.End.X - 108f, rect.End.Y - 96f),
			new Vector2(rect.GetCenter().X - 132f, rect.GetCenter().Y + 24f),
			new Vector2(rect.GetCenter().X + 126f, rect.GetCenter().Y - 18f),
			new Vector2(rect.GetCenter().X, rect.Position.Y + 84f),
			new Vector2(rect.GetCenter().X, rect.End.Y - 84f),
		];

		int offsetSeed = (nodeId * 31 + slotIndex * 17) % anchors.Length;
		Vector2 basePos = anchors[offsetSeed];
		Vector2 jitter = new(((nodeId + slotIndex * 3) % 5 - 2) * 10f, ((nodeId * 2 + slotIndex) % 5 - 2) * 8f);
		return ClampToRoom(basePos + jitter);
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

	private void EnterNodeRoom(int nodeId, Vector2 entryDirection, bool advanceTurn)
	{
		int previousNodeId = _playerNodeId;
		_playerNodeId = nodeId;
		_nodes[_playerNodeId].Visited = true;
		UpdateVision(_playerNodeId);
		_pendingExitNodeId = -1;
		_heroHasMoveTarget = false;

		Vector2 heroSpawn = GetDoorSpawnPoint(entryDirection);
		Vector2 inwardFacing = entryDirection == Vector2.Zero ? Vector2.Right : -entryDirection.Normalized();
		if (_roomUnits.Count == 0)
		{
			SpawnAlliesAt(heroSpawn);
			RoomUnit spawnedHero = FindHeroUnit();
			if (spawnedHero != null)
			{
				spawnedHero.Facing = inwardFacing;
			}
		}
		else
		{
			RoomUnit hero = FindHeroUnit();
			if (hero != null)
			{
				hero.Position = heroSpawn;
				hero.Facing = inwardFacing;
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

	private Vector2 GetDoorSpawnPoint(Vector2 direction)
	{
		return ClampToRoom(GetRoomExitRect(direction).GetCenter());
	}

	private void SpawnAlliesAt(Vector2 heroPos)
	{
		_roomUnits.Clear();
		if (EnableCombatFxDebugOpening && _playerNodeId == 0)
		{
			SpawnDebugAlliesAt(heroPos);
			return;
		}

		RoomUnit hero = CreateRoomUnit(true, true, false, false, true, "英雄", heroPos);
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
			RoomUnit soldier = CreateRoomUnit(true, false, false, false, false, _runSoldiers[i].Name, ClampToRoom(heroPos + offset));
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
		RoomUnit ranger = CreateRoomUnit(true, true, false, false, true, "蓝羽试射手", ClampToRoom(heroPos + new Vector2(-12f, -26f)));
		ranger.Hp = 260;
		ranger.MaxHp = 260;
		ranger.DamageMin = 2;
		ranger.DamageMax = 4;
		ranger.AttackRange = 196f;
		ranger.Speed = 118f;
		ranger.AttackCycleScale = 2f;
		_roomUnits.Add(ranger);

		RoomUnit vanguard = CreateRoomUnit(true, false, true, false, false, "前锋测试机", ClampToRoom(heroPos + new Vector2(-28f, 26f)));
		vanguard.Hp = 300;
		vanguard.MaxHp = 300;
		vanguard.DamageMin = 2;
		vanguard.DamageMax = 4;
		vanguard.AttackRange = 28f;
		vanguard.Speed = 120f;
		vanguard.AttackCycleScale = 2f;
		_roomUnits.Add(vanguard);
	}

	private RoomUnit CreateRoomUnit(bool isPlayerSide, bool isHero, bool isElite, bool isAiSquad, bool isRanged, string name, Vector2 position)
	{
		return new RoomUnit
		{
			IsPlayerSide = isPlayerSide,
			IsHero = isHero,
			IsElite = isElite,
			IsAiSquad = isAiSquad,
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
			CombatState = isRanged ? RoomCombatState.Idle : RoomCombatState.Advance,
			TacticalAnchor = position,
			MaxStamina = isRanged ? 0f : (isHero ? 88f : (isElite ? 80f : 72f)),
			Stamina = isRanged ? 0f : (isHero ? 88f : (isElite ? 80f : 72f)),
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
		RoomUnit ranger = CreateRoomUnit(false, false, true, false, true, "赤眼试射手", ClampToRoom(new Vector2(rect.End.X - 120f, rect.GetCenter().Y - 26f)));
		ranger.Hp = 260;
		ranger.MaxHp = 260;
		ranger.DamageMin = 2;
		ranger.DamageMax = 4;
		ranger.AttackRange = 196f;
		ranger.Speed = 118f;
		ranger.AttackCycleScale = 2f;
		_roomUnits.Add(ranger);

		RoomUnit raider = CreateRoomUnit(false, false, true, false, false, "斩刃试作体", ClampToRoom(new Vector2(rect.End.X - 148f, rect.GetCenter().Y + 28f)));
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
			RoomUnit enemy = CreateRoomUnit(false, false, false, false, i % 3 == 0, "守军", p);
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
		RoomUnit elite = CreateRoomUnit(false, false, true, true, true, $"{squad.Name} 队长", ClampToRoom(new Vector2(rect.End.X - 76f, rect.GetCenter().Y - 26f)));
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
			RoomUnit enemy = CreateRoomUnit(false, false, false, true, i % 3 == 1, "敌兵", p);
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
		unit.HitFlash = Mathf.Max(0f, unit.HitFlash - delta);
		unit.StaggerTime = Mathf.Max(0f, unit.StaggerTime - delta);
		unit.RecoveryTime = Mathf.Max(0f, unit.RecoveryTime - delta);
		unit.KnockbackTime = Mathf.Max(0f, unit.KnockbackTime - delta);
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
				BeginRoomAttack(attacker, target, 28f);
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
			BeginRoomAttack(attacker, target, 28f);
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

	private void BeginRoomAttack(RoomUnit attacker, RoomUnit target, float rangeSlack)
	{
		attacker.PendingAttackTarget = target;
		attacker.PendingAttackDamage = _rng.RandiRange(attacker.DamageMin, attacker.DamageMax);
		attacker.PendingAttackHeavy = attacker.IsHero || attacker.IsElite;
		attacker.PendingAttackRangeSlack = rangeSlack;
		attacker.PendingAttackLungeDistance = 0f;
		attacker.AttackWindupTime = attacker.IsRanged ? 0.13f : (attacker.PendingAttackHeavy ? 0.12f : 0.09f);
		attacker.RecoveryTime = attacker.IsRanged ? 0.1f : 0.08f;
		float baseCooldown = attacker.IsRanged ? 0.42f : (attacker.PendingAttackHeavy ? 0.54f : 0.46f);
		attacker.AttackCooldown = baseCooldown * Mathf.Max(0.1f, attacker.AttackCycleScale);
		attacker.CombatState = RoomCombatState.AttackCommit;
		attacker.CombatStateTimer = attacker.AttackWindupTime + attacker.RecoveryTime;
		attacker.TacticalAnchor = attacker.Position;
	}

	private float GetRoomMoveSpeed(RoomUnit unit, bool sprint, float delta, float scale)
	{
		float speed = unit.Speed * scale;
		if (!sprint || unit.MaxStamina <= 0f || unit.Stamina <= 0f)
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
		PlayAttackSfx(attacker.IsRanged, heavy);
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
	}

	private void ApplyProjectileOrSlashHit(RoomUnit target, int damage, Vector2 dir, bool heavy, bool rangedHit)
	{
		if (target == null || !target.IsAlive)
		{
			return;
		}

		target.Hp = Mathf.Max(0, target.Hp - damage);
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

		int index = 0;
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsPlayerSide || unit.IsHero || !unit.IsAlive)
			{
				continue;
			}

			unit.Position = GetAllyFollowAnchor(hero, unit);
			index++;
		}
	}

	private Vector2 GetExitDirection(MapNode fromNode, int linkedNodeId)
	{
		if (linkedNodeId < 0 || linkedNodeId >= _nodes.Count)
		{
			return Vector2.Right;
		}

		Vector2 delta = _nodes[linkedNodeId].Position - fromNode.Position;
		return delta == Vector2.Zero ? Vector2.Right : delta.Normalized();
	}

	private Vector2 GetOppositeDirection(Vector2 direction)
	{
		return direction == Vector2.Zero ? Vector2.Left : -direction.Normalized();
	}

	private Rect2 GetMapCanvasRect()
	{
		float titleHeight = _showMapOverlay ? Ui(96f) : Ui(24f);
		float bottomPadding = Ui(22f);
		return new Rect2(
			_mapRect.Position + new Vector2(Ui(18f), titleHeight),
			_mapRect.Size - new Vector2(Ui(36f), titleHeight + bottomPadding));
	}

	private Vector2 GetMapScale()
	{
		Rect2 canvas = GetMapCanvasRect();
		return new Vector2(
			canvas.Size.X / DesignMapRect.Size.X,
			canvas.Size.Y / DesignMapRect.Size.Y);
	}

	private float GetMapUnitScale()
	{
		Vector2 scale = GetMapScale();
		return Mathf.Min(scale.X, scale.Y);
	}

	private Vector2 MapToScreen(Vector2 point)
	{
		Rect2 canvas = GetMapCanvasRect();
		Vector2 scale = GetMapScale();
		return canvas.Position + new Vector2(
			(point.X - DesignMapRect.Position.X) * scale.X,
			(point.Y - DesignMapRect.Position.Y) * scale.Y);
	}

	private Rect2 MapToScreen(Rect2 rect)
	{
		Vector2 min = MapToScreen(rect.Position);
		Vector2 max = MapToScreen(rect.End);
		return new Rect2(min, max - min);
	}

	private Rect2 GetRoomExitRect(MapNode node, int linkedNodeId)
	{
		return GetRoomExitRect(GetExitDirection(node, linkedNodeId));
	}

	private Rect2 GetRoomExitRect(Vector2 rawDirection)
	{
		Vector2 size = new(112f, 46f);
		Rect2 arena = GetRoomArenaRect();
		Vector2 center = arena.GetCenter();
		Vector2 direction = rawDirection == Vector2.Zero ? Vector2.Right : rawDirection.Normalized();
		float insetX = arena.Size.X * 0.5f - size.X * 0.5f - 22f;
		float insetY = arena.Size.Y * 0.5f - size.Y * 0.5f - 22f;
		float tx = Mathf.Abs(direction.X) > 0.001f ? insetX / Mathf.Abs(direction.X) : float.MaxValue;
		float ty = Mathf.Abs(direction.Y) > 0.001f ? insetY / Mathf.Abs(direction.Y) : float.MaxValue;
		float distance = Mathf.Min(tx, ty);
		Vector2 doorCenter = center + direction * distance;
		return new Rect2(doorCenter - size * 0.5f, size);
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
			DrawString(ThemeDB.FallbackFont, unit.Position + new Vector2(-28f, -20f), unit.Name, HorizontalAlignment.Left, 110f, UiFont(12), Colors.White);
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
		float sizeScale = unit.IsHero ? 1.46f : (unit.IsElite ? 1.34f : 1.1f);
		float torsoHalfWidth = (unit.IsRanged ? 4.2f : 5.2f) * sizeScale;
		float torsoHeight = (unit.IsHero ? 13f : 11f) * sizeScale;
		float shoulderWidth = torsoHalfWidth + (unit.IsElite ? 1.3f : 0.7f) * sizeScale;

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

		float headRadius = (unit.IsHero ? 4.8f : 4.1f) * sizeScale;
		DrawCircle(headCenter, headRadius + 1.2f, outline);
		DrawCircle(headCenter, headRadius, bodyColor.Lerp(Colors.White, unit.IsRanged ? 0.16f : 0.08f));
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

		if (unit.IsRanged)
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
		float slashBias = attackPose;
		Vector2 weaponBase = handFront + new Vector2(faceSide.X * 1.6f, -0.8f);
		Vector2 weaponTip = weaponBase + new Vector2(faceSide.X * (10f + slashBias * 5f), -4.5f - slashBias * 4.2f);
		DrawLine(weaponBase, weaponTip, outline, 3f);
		DrawLine(weaponBase, weaponTip, accent, 1.6f);

		Vector2 guardA = weaponBase + new Vector2(0f, -2.6f);
		Vector2 guardB = weaponBase + new Vector2(0f, 2.6f);
		DrawLine(guardA, guardB, outline, 2f);
		DrawLine(guardA, guardB, accent.Lerp(Colors.White, 0.2f), 1f);

		Vector2 shieldCenter = handBack + new Vector2(-faceSide.X * 3.2f, 0.8f);
		float shieldH = unit.IsElite ? 5.8f : 5.1f;
		float shieldW = unit.IsElite ? 3.9f : 3.4f;
		Vector2[] shield =
		[
			shieldCenter + new Vector2(0f, -shieldH),
			shieldCenter + new Vector2(shieldW, -shieldH * 0.2f),
			shieldCenter + new Vector2(0f, shieldH),
			shieldCenter + new Vector2(-shieldW, -shieldH * 0.2f),
		];
		DrawColoredPolygon(shield, accent.Lerp(Colors.Black, 0.28f));
		DrawPolyline(new[] { shield[0], shield[1], shield[2], shield[3], shield[0] }, outline, 1.2f);
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

	}

	private static bool IsTriangleDrawable(Vector2 a, Vector2 b, Vector2 c)
	{
		float twiceArea = Mathf.Abs((b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
		return twiceArea > 0.02f;
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

		if (IsOverloaded())
		{
			_status = "队伍当前超载，无法离开这个房间。";
			return;
		}

		Vector2 direction = GetExitDirection(node, nodeId);
		Rect2 doorRect = GetRoomExitRect(node, nodeId);
		_pendingExitNodeId = nodeId;
		_pendingExitDirection = direction;
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
			_overflowBackpackItems.Clear();
			_hasDraggedBackpackItem = false;
			_draggedBackpackItem = null;
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
		LootContainer pile = new()
		{
			Label = "尸体堆",
			Kind = ContainerKind.CorpsePile,
			Position = ClampToRoom(GetRoomArenaRect().GetCenter() + new Vector2(36f, 48f)),
			Tint = new Color(0.72f, 0.74f, 0.78f, 1f),
			AutoOpenRange = 58f,
		};
		int count = _rng.RandiRange(3, 5);
		for (int i = 0; i < count; i++)
		{
			pile.HiddenItems.Add(squad != null && squad.Loot.Count > 0 ? TakeRandomLoot(squad) : RollLootItem());
		}
		node.Containers.Add(pile);

		LootContainer elite = new()
		{
			Label = squad != null ? $"{squad.Name} 队长" : "精英守卫",
			Kind = ContainerKind.EliteCorpse,
			Position = ClampToRoom(GetRoomArenaRect().GetCenter() + new Vector2(96f, -12f)),
			Tint = squad != null ? new Color(0.98f, 0.64f, 0.26f, 1f) : new Color(0.86f, 0.84f, 0.8f, 1f),
			AutoOpenRange = 60f,
		};
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
			AcquiredInRun = true,
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
		LootContainer pile = new()
		{
			Label = $"{loser.Name} 的遗骸",
			Kind = ContainerKind.CorpsePile,
			Position = ClampToRoom(GetRoomArenaRect().GetCenter()),
			Tint = new Color(0.92f, 0.5f, 0.22f, 1f),
			AutoOpenRange = 58f,
		};
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
				case "select_stash":
					_selectedStashIndex = button.Index;
					_selectedShopIndex = -1;
					return;
				case "select_shop":
					_selectedShopIndex = button.Index;
					_selectedStashIndex = -1;
					_selectedLoadoutIndex = -1;
					return;
				case "select_loadout":
					_selectedLoadoutIndex = button.Index;
					_selectedStashIndex = -1;
					_selectedShopIndex = -1;
					return;
				case "auto_pack_hideout":
					RepackHideoutLoadout();
					_status = "已整理局外背包。";
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
			else if (button.Action == "open_settlement" && !_runFailed)
			{
				_showSettlementTransfer = true;
				_selectedSettlementIndex = -1;
				_selectedStashIndex = -1;
				_selectedShopIndex = -1;
				_selectedLoadoutIndex = -1;
			}
			else if (button.Action == "finish_settlement" && !_runFailed)
			{
				InitHideout();
				_status = "已完成结算，返回局外整备。";
			}
			else if (button.Action == "auto_pack_settlement" && !_runFailed)
			{
				RepackHideoutLoadout();
				_status = "已整理局外背包。";
			}
			else if (button.Action == "settlement_all_to_stash" && !_runFailed)
			{
				MoveAllSettlementItemsToStash();
			}
			else if (button.Action == "select_settlement")
			{
				_selectedSettlementIndex = button.Index;
				_selectedStashIndex = -1;
				_selectedShopIndex = -1;
				_selectedLoadoutIndex = -1;
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
			case "take_all":
				TakeAllFromContainer(button.Index);
				break;
			case "drop_overflow":
				DropOverflowToGround();
				break;
			case "drop_dragged_backpack":
				DropDraggedBackpackItemToGround();
				break;
			case "open_container":
				OpenContainer(button.Index);
				break;
			case "extract":
				TryExtract();
				break;
			case "auto_pack":
				AutoOrganizeBackpack();
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

			if (!AddLoot(equipped.Label))
			{
				return;
			}

			equipped.Taken = true;
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

				if (!AddLoot(gridItem.Label, gridItem.Size, gridItem.AcquiredInRun))
			{
				return;
			}

			gridItem.Taken = true;
			LogEvent($"从 {container.Label} 取走了 {gridItem.Label}。");
			RefreshStatus();
			return;
		}

		if (itemIndex < 0 || itemIndex >= container.VisibleItems.Count)
		{
			return;
		}

		string item = container.VisibleItems[itemIndex];
		if (!AddLoot(item))
		{
			return;
		}

		container.VisibleItems.RemoveAt(itemIndex);
		LogEvent($"从 {container.Label} 取走了 {item}。");
		RefreshStatus();
	}

	private void TakeAllFromContainer(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "房间仍然危险，暂时不能搜刮。";
			return;
		}

		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		int takenCount = 0;

		for (int equipIndex = 0; equipIndex < container.EquippedItems.Count; equipIndex++)
		{
			EquippedLoot equipped = container.EquippedItems[equipIndex];
			if (equipped.Taken || string.IsNullOrEmpty(equipped.Label))
			{
				continue;
			}

			if (!AddLoot(equipped.Label))
			{
				break;
			}

			equipped.Taken = true;
			takenCount++;
		}

		if (container.GridItems.Count > 0)
		{
			for (int itemIndex = 0; itemIndex < container.GridItems.Count; itemIndex++)
			{
				GridLootItem item = container.GridItems[itemIndex];
				if (!item.Revealed || item.Taken)
				{
					continue;
				}

				if (!AddLoot(item.Label, item.Size, item.AcquiredInRun))
				{
					break;
				}

				item.Taken = true;
				takenCount++;
			}
		}
		else
		{
			for (int i = container.VisibleItems.Count - 1; i >= 0; i--)
			{
				string item = container.VisibleItems[i];
				if (!AddLoot(item))
				{
					break;
				}

				container.VisibleItems.RemoveAt(i);
				takenCount++;
			}
		}

		if (takenCount <= 0)
		{
			_status = $"{container.Label} 当前没有可一键拿取的物品。";
			return;
		}

		LogEvent($"从 {container.Label} 一次取走了 {takenCount} 件物品。");
		RefreshStatus();
	}

	private void TryExtract()
	{
		if (_nodes[_playerNodeId].Type != NodeType.Extract)
		{
			_status = "当前位置不是撤离点。";
			return;
		}

		if (IsOverloaded())
		{
			_status = "队伍当前超载，无法执行撤离。";
			return;
		}

		List<BackpackItem> extractedItems = new();
		for (int i = 0; i < _runBackpack.Count; i++)
		{
			extractedItems.Add(CloneBackpackItem(_runBackpack[i]));
		}
		for (int i = 0; i < _overflowBackpackItems.Count; i++)
		{
			extractedItems.Add(CloneBackpackItem(_overflowBackpackItems[i]));
		}

		if (!CanFitItemsInHideoutLoadout(extractedItems))
		{
			_status = "局外背包空间不足，当前无法撤离。";
			return;
		}

		RebuildHideoutLoadout(extractedItems);
		_runBackpack.Clear();
		_overflowBackpackItems.Clear();
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_runEnded = true;
		_runFailed = false;
		_showSettlementTransfer = false;
		_selectedSettlementIndex = -1;
		_status = "撤离成功，等待结算转移。";
		LogEvent("玩家成功撤离。");
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
			count += CountNodeLootSingle(container);
		}
		return count;
	}

	private int CountNodeLootSingle(LootContainer container)
	{
		int count = 0;
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
}


