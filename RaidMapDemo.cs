using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RaidMapDemo : Node2D
{
	private const int RecruitCost = 30;
	private const int MapTemplateCount = 2;
	private const int DifficultyCount = 3;
	private const bool EnableCombatFxDebugOpening = false;
	private const bool EnableSoldierBonusStartingXp = true;
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

	private enum SoldierClass
	{
		Recruit,
		Shield,
		EliteShield,
		Pike,
		Blade,
		Archer,
		Cavalry,
	}

	private enum SoldierActiveSkill
	{
		None,
		Sprint,
		ShieldRush,
	}

	private enum SoldierPassiveSkill
	{
		None,
		MissileGuard,
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
		public SoldierClass Class = SoldierClass.Recruit;
		public int Experience;
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
		public string RosterName = "";
		public SoldierClass SoldierClass;
		public Vector2 Position;
		public Vector2 Facing = Vector2.Right;
		public float Speed;
		public int Hp;
		public int MaxHp;
		public int DamageMin;
		public int DamageMax;
		public int Armor;
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
		public bool CanSprint = true;
		public SoldierActiveSkill ActiveSkill;
		public SoldierPassiveSkill PassiveSkill;
		public float ActiveSkillCooldown;
		public float SkillMoveTime;
		public float SkillMoveSpeed;
		public Vector2 SkillMoveDirection;
		public float ShieldRushContactLock;
		public float ProjectileDamageScale = 1f;
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
	private int _selectedSoldierIndex = -1;
	private int _soldierRosterPage;
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
		_selectedSoldierIndex = -1;
		_soldierRosterPage = 0;
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
			_runSoldiers.Add(CloneSoldierRecord(soldier));
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
			_runSoldiers.Add(new SoldierRecord { Name = "前锋测试机", Class = SoldierClass.Blade, Experience = 0 });
		}

		RecalculatePlayerStrength();

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
		_selectedSoldierIndex = -1;
		_soldierRosterPage = 0;
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
			_soldierRoster.Add(CloneSoldierRecord(_runSoldiers[i]));
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
			int column = i % 2;
			int row = i / 2;
			Vector2 offset = new Vector2(-42f - column * 34f, row * 28f - 34f);
			RoomUnit soldier = CreateRoomUnit(true, false, false, false, IsSoldierRangedClass(_runSoldiers[i].Class), _runSoldiers[i].Name, ClampToRoom(heroPos + offset));
			soldier.RosterName = _runSoldiers[i].Name;
			ApplySoldierClassToRoomUnit(soldier, _runSoldiers[i]);
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
		if (attacker.SoldierClass != SoldierClass.Shield
			|| attacker.ActiveSkill != SoldierActiveSkill.ShieldRush
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
		}
	}

	private void ApplyShieldRushHits(RoomUnit attacker, bool impact)
	{
		float radius = impact ? 24f : 16f;
		int damage = impact ? Mathf.Max(1, attacker.DamageMax) : Mathf.Max(1, attacker.DamageMin);
		float stagger = impact ? 0.6f : 0.3f;
		float hitPause = impact ? 0.08f : 0.04f;
		float force = impact ? 1220f : 610f;
		float duration = impact ? 1.08f : 0.54f;
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
		attacker.PendingAttackHeavy = attacker.IsHero || attacker.IsElite || attacker.SoldierClass == SoldierClass.Cavalry;
		attacker.PendingAttackRangeSlack = rangeSlack;
		attacker.PendingAttackLungeDistance = attacker.SoldierClass == SoldierClass.Cavalry ? 16f : 0f;
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
		bool eliteSoldierClass = unit.SoldierClass == SoldierClass.EliteShield;
		float sizeScale = unit.IsHero ? 1.46f : ((unit.IsElite || eliteSoldierClass) ? 1.34f : 1.1f);
		float torsoHalfWidth = (unit.IsRanged ? 4.2f : 5.2f) * sizeScale;
		float torsoHeight = (unit.IsHero ? 13f : 11f) * sizeScale;
		float shoulderWidth = torsoHalfWidth + ((unit.IsElite || eliteSoldierClass) ? 1.3f : 0.7f) * sizeScale;

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
		if (eliteSoldierClass)
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
		if (eliteSoldierClass)
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
		DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, unit.IsElite);
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
				DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, unit.IsElite || unit.SoldierClass == SoldierClass.EliteShield);
				break;
			case SoldierClass.EliteShield:
				DrawRoomShieldSilhouette(handFront, handBack, faceSide, outline, accent, attackPose, true);
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

	private void DrawRoomShieldSilhouette(Vector2 handFront, Vector2 handBack, Vector2 faceSide, Color outline, Color accent, float attackPose, bool eliteShield)
	{
		Vector2 shieldCenter = handFront + new Vector2(faceSide.X * (2.2f + attackPose * 0.45f), 2.9f - attackPose * 0.2f);
		float shieldH = eliteShield ? 14.6f : 13.2f;
		float shieldW = eliteShield ? 6.7f : 6f;
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
				case "select_soldier":
					_selectedSoldierIndex = button.Index;
					_selectedStashIndex = -1;
					_selectedShopIndex = -1;
					_selectedLoadoutIndex = -1;
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
				case "soldier_page_prev":
					_soldierRosterPage = Mathf.Max(0, _soldierRosterPage - 1);
					return;
				case "soldier_page_next":
					_soldierRosterPage = Mathf.Min(Mathf.Max(0, (_soldierRoster.Count - 1) / 3), _soldierRosterPage + 1);
					return;
				case "promote_shield":
					PromoteSelectedSoldier(SoldierClass.Shield);
					return;
				case "promote_elite_shield":
					PromoteSelectedSoldier(SoldierClass.EliteShield);
					return;
				case "promote_pike":
					PromoteSelectedSoldier(SoldierClass.Pike);
					return;
				case "promote_blade":
					PromoteSelectedSoldier(SoldierClass.Blade);
					return;
				case "promote_archer":
					PromoteSelectedSoldier(SoldierClass.Archer);
					return;
				case "promote_cavalry":
					PromoteSelectedSoldier(SoldierClass.Cavalry);
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
		GrantRunSoldierExperience(1, "成功撤离");
		CommitRunSoldierRoster();
		_status = "撤离成功，等待结算转移。";
		LogEvent("玩家成功撤离。");
	}

	private bool CanSearch(MapNode node)
	{
		return !_runEnded && !_inHideout && !HasHostilesInRoom();
	}

	private bool AddLoot(string item)
	{
		return AddLoot(item, GetBackpackItemSize(item), true);
	}

	private bool TryAddToStash(string item)
	{
		return TryAddToStash(CreateBackpackItem(item, GetBackpackItemSize(item)));
	}

	private bool TryAddToStash(BackpackItem item)
	{
		BackpackItem stashItem = CloneBackpackItem(item);
		if (!TryPlaceStorageItem(stashItem, _stash, StashGridWidth, StashGridHeight))
		{
			return false;
		}

		_stash.Add(stashItem);
		return true;
	}

	private bool CanFitItemsInStash(List<BackpackItem> items)
	{
		List<BackpackItem> staged = new();
		for (int i = 0; i < _stash.Count; i++)
		{
			staged.Add(CloneBackpackItem(_stash[i]));
		}

		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem item = CloneBackpackItem(items[i]);
			if (!TryPlaceStorageItem(item, staged, StashGridWidth, StashGridHeight))
			{
				return false;
			}

			staged.Add(item);
		}

		return true;
	}

	private bool CanFitItemsInHideoutLoadout(List<BackpackItem> items)
	{
		List<BackpackItem> staged = new();
		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem item = CloneBackpackItem(items[i]);
			if (!TryPlaceHideoutLoadoutItemInList(item, staged))
			{
				return false;
			}

			staged.Add(item);
		}

		return true;
	}

	private void RebuildHideoutLoadout(List<BackpackItem> items)
	{
		_hideoutLoadout.Clear();
		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem item = CloneBackpackItem(items[i]);
			if (TryPlaceHideoutLoadoutItemInList(item, _hideoutLoadout))
			{
				_hideoutLoadout.Add(item);
			}
		}

		RefreshLootValueFromCurrentInventory();
	}

	private bool HandleOpenContainerPopupClick(Vector2 click)
	{
		if (!_hasDraggedBackpackItem || _selectedContainerIndex < 0 || _inHideout || _runEnded)
		{
			return false;
		}

		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex >= node.Containers.Count)
		{
			return false;
		}

		LootContainer container = node.Containers[_selectedContainerIndex];
		GetContainerPopupGridLayout(container, out Rect2 panel, out Vector2 gridOrigin, out Vector2 cellSize);
		if (!panel.HasPoint(click))
		{
			return false;
		}

		Rect2 gridRect = new(gridOrigin, new Vector2(container.GridSize.X * cellSize.X, container.GridSize.Y * cellSize.Y));
		if (!gridRect.HasPoint(click))
		{
			return true;
		}

		Vector2I cell = GetDraggedBackpackTargetCell(gridOrigin, cellSize);
		if (!TryMoveDraggedBackpackItemToContainer(container, cell))
		{
			_status = "容器空间不足，无法放回。";
		}

		return true;
	}

	private bool TryMoveDraggedBackpackItemToContainer(LootContainer container, Vector2I cell)
	{
		if (!_hasDraggedBackpackItem || _draggedBackpackItem == null)
		{
			return false;
		}

		GridLootItem movedItem = new()
		{
			Label = _draggedBackpackItem.Label,
			Rarity = _draggedBackpackItem.Rarity,
			Size = _draggedBackpackItem.Size,
			Cell = cell,
			AcquiredInRun = _draggedBackpackItem.AcquiredInRun,
			Revealed = true,
			SearchTime = GetGridSearchTime(_draggedBackpackItem.Rarity),
		};

		bool placed = false;
		if (cell.X >= 0 && cell.Y >= 0
			&& cell.X + movedItem.Size.X <= container.GridSize.X
			&& cell.Y + movedItem.Size.Y <= container.GridSize.Y
			&& IsGridAreaFree(container, cell, movedItem.Size))
		{
			movedItem.Cell = cell;
			placed = true;
		}
		else if (movedItem.Size.X != movedItem.Size.Y)
		{
			Vector2I rotated = new(movedItem.Size.Y, movedItem.Size.X);
			if (cell.X >= 0 && cell.Y >= 0
				&& cell.X + rotated.X <= container.GridSize.X
				&& cell.Y + rotated.Y <= container.GridSize.Y
				&& IsGridAreaFree(container, cell, rotated))
			{
				movedItem.Size = rotated;
				movedItem.Cell = cell;
				placed = true;
			}
		}

		if (!placed)
		{
			if (!TryPlaceGridItem(container, movedItem))
			{
				if (movedItem.Size.X == movedItem.Size.Y)
				{
					return false;
				}

				movedItem.Size = new Vector2I(movedItem.Size.Y, movedItem.Size.X);
				if (!TryPlaceGridItem(container, movedItem))
				{
					return false;
				}
			}
		}

		container.GridItems.Add(movedItem);
		LogEvent($"将 {_draggedBackpackItem.Label} 放回了 {container.Label}。");
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
		RefreshStatus();
		return true;
	}

	private bool AddLoot(string item, Vector2I size)
	{
		return AddLoot(item, size, true);
	}

	private bool AddLoot(string item, Vector2I size, bool acquiredInRun)
	{
		BackpackItem backpackItem = CreateBackpackItem(item, size, acquiredInRun);
		if (TryPlaceBackpackItem(backpackItem))
		{
			_runBackpack.Add(backpackItem);
			RefreshLootValueFromCurrentInventory();
			return true;
		}

		_overflowBackpackItems.Add(backpackItem);
		AutoOrganizeBackpack();
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private BackpackItem CreateBackpackItem(string item, Vector2I size, bool acquiredInRun = false)
	{
		return new BackpackItem
		{
			Label = item,
			Rarity = GetItemRarityByLabel(item),
			Size = size,
			AcquiredInRun = acquiredInRun,
		};
	}

	private Vector2I GetBackpackItemSize(string item)
	{
		if (item.Contains("锁甲") || item.Contains("护甲") || item.Contains("武装"))
		{
			return new Vector2I(2, 2);
		}

		if (item.Contains("长枪"))
		{
			return new Vector2I(1, 3);
		}

		if (item.Contains("军刀") || item.Contains("佩刀"))
		{
			return new Vector2I(1, 2);
		}

		if (item.Contains("包") || item.Contains("口粮") || item.Contains("草药"))
		{
			return new Vector2I(2, 1);
		}

		return new Vector2I(1, 1);
	}

	private bool TryPlaceStorageItem(BackpackItem item, List<BackpackItem> items, int gridWidth, int gridHeight)
	{
		if (TryPlaceStorageItemWithSize(item, item.Size, items, gridWidth, gridHeight))
		{
			return true;
		}

		if (item.Size.X != item.Size.Y)
		{
			Vector2I rotated = new(item.Size.Y, item.Size.X);
			if (TryPlaceStorageItemWithSize(item, rotated, items, gridWidth, gridHeight))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryPlaceStorageItemWithSize(BackpackItem item, Vector2I size, List<BackpackItem> items, int gridWidth, int gridHeight)
	{
		for (int y = 0; y <= gridHeight - size.Y; y++)
		{
			for (int x = 0; x <= gridWidth - size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsStorageAreaFree(cell, size, items))
				{
					continue;
				}

				item.Size = size;
				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsStorageAreaFree(Vector2I cell, Vector2I size, List<BackpackItem> items)
	{
		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem existing = items[i];
			bool overlapX = cell.X < existing.Cell.X + existing.Size.X && cell.X + size.X > existing.Cell.X;
			bool overlapY = cell.Y < existing.Cell.Y + existing.Size.Y && cell.Y + size.Y > existing.Cell.Y;
			if (overlapX && overlapY)
			{
				return false;
			}
		}

		return true;
	}

	private bool TryPlaceHideoutLoadoutItemInList(BackpackItem item, List<BackpackItem> items)
	{
		List<BackpackCapacityBlock> blocks = BuildHideoutLoadoutCapacityBlocks();
		for (int y = 0; y <= TeamBackpackMaxRows - item.Size.Y; y++)
		{
			for (int x = 0; x <= TeamBackpackMaxWidth - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsAreaEnabledInBlocks(cell, item.Size, blocks) || !IsStorageAreaFree(cell, item.Size, items))
				{
					continue;
				}

				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool TryPlaceBackpackItem(BackpackItem item)
	{
		for (int y = 0; y <= TeamBackpackMaxRows - item.Size.Y; y++)
		{
			for (int x = 0; x <= TeamBackpackMaxWidth - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsBackpackAreaEnabled(cell, item.Size) || !IsBackpackAreaFree(cell, item.Size))
				{
					continue;
				}

				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsBackpackAreaEnabled(Vector2I cell, Vector2I size)
	{
		for (int y = cell.Y; y < cell.Y + size.Y; y++)
		{
			for (int x = cell.X; x < cell.X + size.X; x++)
			{
				if (!IsCellEnabledInBlocks(new Vector2I(x, y), BuildCurrentBackpackCapacityBlocks()))
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool IsBackpackCellEnabled(Vector2I cell)
	{
		return IsCellEnabledInBlocks(cell, BuildCurrentBackpackCapacityBlocks());
	}

	private bool IsCellEnabledInBlocks(Vector2I cell, List<BackpackCapacityBlock> blocks)
	{
		foreach (BackpackCapacityBlock block in blocks)
		{
			if (cell.X >= block.Cell.X && cell.X < block.Cell.X + block.Size.X
				&& cell.Y >= block.Cell.Y && cell.Y < block.Cell.Y + block.Size.Y)
			{
				return true;
			}
		}

		return false;
	}

	private bool IsAreaEnabledInBlocks(Vector2I cell, Vector2I size, List<BackpackCapacityBlock> blocks)
	{
		for (int y = cell.Y; y < cell.Y + size.Y; y++)
		{
			for (int x = cell.X; x < cell.X + size.X; x++)
			{
				if (!IsCellEnabledInBlocks(new Vector2I(x, y), blocks))
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool IsBackpackAreaFree(Vector2I cell, Vector2I size)
	{
		foreach (BackpackItem existing in _runBackpack)
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

	private int GetCurrentCarryUsage()
	{
		int used = 0;
		foreach (BackpackItem item in _runBackpack)
		{
			used += item.Size.X * item.Size.Y;
		}
		foreach (BackpackItem item in _overflowBackpackItems)
		{
			used += item.Size.X * item.Size.Y;
		}

		return used;
	}

	private int GetCurrentCarryLimit()
	{
		int total = 0;
		foreach (BackpackCapacityBlock block in BuildCurrentBackpackCapacityBlocks())
		{
			total += block.Size.X * block.Size.Y;
		}

		return total;
	}

	private List<BackpackCapacityBlock> BuildCurrentBackpackCapacityBlocks()
	{
		List<BackpackCapacityBlock> blocks = new();
		foreach (RoomUnit unit in _roomUnits)
		{
			if (!unit.IsAlive || !unit.IsPlayerSide)
			{
				continue;
			}

			BackpackCapacityBlock block = new()
			{
				SourceLabel = unit.Name,
				Size = unit.IsHero ? new Vector2I(2, 2) : new Vector2I(1, 1),
			};
			blocks.Add(block);
		}

		ArrangeCapacityBlocks(blocks);
		return blocks;
	}

	private List<BackpackCapacityBlock> BuildHideoutLoadoutCapacityBlocks()
	{
		List<BackpackCapacityBlock> blocks = new()
		{
			new BackpackCapacityBlock
			{
				SourceLabel = "Hero",
				Size = new Vector2I(2, 2),
			}
		};

		for (int i = 0; i < _soldierRoster.Count; i++)
		{
			blocks.Add(new BackpackCapacityBlock
			{
				SourceLabel = _soldierRoster[i].Name,
				Size = new Vector2I(1, 1),
			});
		}

		ArrangeCapacityBlocks(blocks);
		return blocks;
	}

	private void ArrangeCapacityBlocks(List<BackpackCapacityBlock> blocks)
	{
		blocks.Sort((a, b) => (b.Size.X * b.Size.Y).CompareTo(a.Size.X * a.Size.Y));
		bool[,] occupied = new bool[TeamBackpackMaxWidth, TeamBackpackMaxRows];
		for (int i = 0; i < blocks.Count; i++)
		{
			BackpackCapacityBlock block = blocks[i];
			if (!TryPlaceCapacityBlock(block, occupied))
			{
				break;
			}
		}
	}

	private bool TryPlaceCapacityBlock(BackpackCapacityBlock block, bool[,] occupied)
	{
		const int groupSize = 4;
		int groupColumns = Mathf.Max(1, TeamBackpackMaxWidth / groupSize);
		int groupRows = Mathf.Max(1, TeamBackpackMaxRows / groupSize);

		for (int groupRow = 0; groupRow < groupRows; groupRow++)
		{
			for (int groupColumn = 0; groupColumn < groupColumns; groupColumn++)
			{
				int originX = groupColumn * groupSize;
				int originY = groupRow * groupSize;
				int occupiedCount = CountCapacityGroupOccupiedCells(occupied, originX, originY, groupSize);
				if (occupiedCount >= groupSize * groupSize)
				{
					continue;
				}

				if (!TryPlaceCapacityBlockInGroup(block, occupied, originX, originY, groupSize))
				{
					continue;
				}

				return true;
			}
		}

		return false;
	}

	private bool TryPlaceCapacityBlockInGroup(BackpackCapacityBlock block, bool[,] occupied, int originX, int originY, int groupSize)
	{
		int maxX = originX + groupSize - block.Size.X;
		int maxY = originY + groupSize - block.Size.Y;
		Vector2I bestCell = new(-1, -1);
		int bestPrimaryScore = int.MaxValue;
		int bestSecondaryScore = int.MaxValue;
		int bestTertiaryScore = int.MaxValue;
		int bestQuaternaryScore = int.MaxValue;
		for (int y = originY; y <= maxY; y++)
		{
			for (int x = originX; x <= maxX; x++)
			{
				if (!IsCapacityBlockAreaFree(occupied, x, y, block.Size))
				{
					continue;
				}

				GetCapacityPlacementScores(occupied, x, y, block.Size, originX, originY, groupSize,
					out int primaryScore, out int secondaryScore, out int tertiaryScore, out int quaternaryScore);
				if (primaryScore > bestPrimaryScore)
				{
					continue;
				}

				if (primaryScore == bestPrimaryScore && secondaryScore > bestSecondaryScore)
				{
					continue;
				}

				if (primaryScore == bestPrimaryScore && secondaryScore == bestSecondaryScore && tertiaryScore > bestTertiaryScore)
				{
					continue;
				}

				if (primaryScore == bestPrimaryScore && secondaryScore == bestSecondaryScore && tertiaryScore == bestTertiaryScore && quaternaryScore >= bestQuaternaryScore)
				{
					continue;
				}

				bestCell = new Vector2I(x, y);
				bestPrimaryScore = primaryScore;
				bestSecondaryScore = secondaryScore;
				bestTertiaryScore = tertiaryScore;
				bestQuaternaryScore = quaternaryScore;
			}
		}

		if (bestCell.X < 0)
		{
			return false;
		}

		block.Cell = bestCell;
		MarkCapacityBlockArea(occupied, block.Cell, block.Size);
		return true;
	}

	private void GetCapacityPlacementScores(bool[,] occupied, int startX, int startY, Vector2I size, int originX, int originY, int groupSize,
		out int primaryScore, out int secondaryScore, out int tertiaryScore, out int quaternaryScore)
	{
		int minUsedX = int.MaxValue;
		int minUsedY = int.MaxValue;
		int maxUsedX = int.MinValue;
		int maxUsedY = int.MinValue;

		for (int y = originY; y < originY + groupSize && y < TeamBackpackMaxRows; y++)
		{
			for (int x = originX; x < originX + groupSize && x < TeamBackpackMaxWidth; x++)
			{
				bool used = occupied[x, y]
					|| (x >= startX && x < startX + size.X && y >= startY && y < startY + size.Y);
				if (!used)
				{
					continue;
				}

				minUsedX = Mathf.Min(minUsedX, x);
				minUsedY = Mathf.Min(minUsedY, y);
				maxUsedX = Mathf.Max(maxUsedX, x);
				maxUsedY = Mathf.Max(maxUsedY, y);
			}
		}

		if (maxUsedX < minUsedX || maxUsedY < minUsedY)
		{
			primaryScore = int.MaxValue;
			secondaryScore = int.MaxValue;
			tertiaryScore = int.MaxValue;
			quaternaryScore = int.MaxValue;
			return;
		}

		int usedWidth = maxUsedX - minUsedX + 1;
		int usedHeight = maxUsedY - minUsedY + 1;
		primaryScore = Mathf.Max(usedWidth, usedHeight);
		secondaryScore = usedWidth * usedHeight;
		tertiaryScore = Mathf.Abs(usedWidth - usedHeight);
		quaternaryScore = (startY - originY) * groupSize + (startX - originX);
	}

	private int CountCapacityGroupOccupiedCells(bool[,] occupied, int originX, int originY, int groupSize)
	{
		int count = 0;
		for (int y = originY; y < originY + groupSize && y < TeamBackpackMaxRows; y++)
		{
			for (int x = originX; x < originX + groupSize && x < TeamBackpackMaxWidth; x++)
			{
				if (occupied[x, y])
				{
					count++;
				}
			}
		}

		return count;
	}

	private bool IsCapacityBlockAreaFree(bool[,] occupied, int startX, int startY, Vector2I size)
	{
		for (int y = startY; y < startY + size.Y; y++)
		{
			for (int x = startX; x < startX + size.X; x++)
			{
				if (x < 0 || y < 0 || x >= TeamBackpackMaxWidth || y >= TeamBackpackMaxRows || occupied[x, y])
				{
					return false;
				}
			}
		}

		return true;
	}

	private void MarkCapacityBlockArea(bool[,] occupied, Vector2I cell, Vector2I size)
	{
		for (int y = cell.Y; y < cell.Y + size.Y; y++)
		{
			for (int x = cell.X; x < cell.X + size.X; x++)
			{
				occupied[x, y] = true;
			}
		}
	}

	private int GetBackpackPreviewGridHeight()
	{
		int maxHeight = 0;
		foreach (BackpackCapacityBlock block in BuildCurrentBackpackCapacityBlocks())
		{
			maxHeight = Mathf.Max(maxHeight, block.Cell.Y + block.Size.Y);
		}

		foreach (BackpackItem item in _runBackpack)
		{
			maxHeight = Mathf.Max(maxHeight, item.Cell.Y + item.Size.Y);
		}

		return Mathf.Clamp(Mathf.Max(1, maxHeight), 1, TeamBackpackMaxRows);
	}

	private bool IsOverloaded()
	{
		return _overflowBackpackItems.Count > 0 || GetCurrentCarryUsage() > GetCurrentCarryLimit();
	}

	private int GetBackpackPreviewGridWidth()
	{
		int maxWidth = 0;
		foreach (BackpackCapacityBlock block in BuildCurrentBackpackCapacityBlocks())
		{
			maxWidth = Mathf.Max(maxWidth, block.Cell.X + block.Size.X);
		}

		foreach (BackpackItem item in _runBackpack)
		{
			maxWidth = Mathf.Max(maxWidth, item.Cell.X + item.Size.X);
		}

		return Mathf.Clamp(Mathf.Max(1, maxWidth), 1, TeamBackpackMaxWidth);
	}

	private bool HandleBackpackPreviewClick(Vector2 click)
	{
		GetBackpackPreviewLayout(out Vector2 gridOrigin, out Vector2 cellSize, out int gridWidth, out int gridHeight);
		Rect2 gridRect = new(gridOrigin, new Vector2(gridWidth * cellSize.X, gridHeight * cellSize.Y));
		if (!gridRect.HasPoint(click))
		{
			if (_hasDraggedBackpackItem)
			{
				RevertDraggedBackpackItem();
				return true;
			}

			return false;
		}

		if (_hasDraggedBackpackItem)
		{
			Vector2I cell = GetBackpackCellAtPoint(click, gridOrigin, cellSize);
			if (TryPlaceDraggedBackpackItem(cell))
			{
				return true;
			}

			RevertDraggedBackpackItem();
			return true;
		}

		int itemIndex = GetBackpackItemIndexAtPoint(click, gridOrigin, cellSize);
		if (itemIndex < 0)
		{
			return false;
		}

		_draggedBackpackItem = _runBackpack[itemIndex];
		_draggedBackpackOriginalCell = _draggedBackpackItem.Cell;
		_draggedBackpackGrabOffset = click - GetBackpackItemRect(_draggedBackpackItem, gridOrigin, cellSize).Position;
		_hasDraggedBackpackItem = true;
		_runBackpack.RemoveAt(itemIndex);
		return true;
	}

	private bool TryPlaceDraggedBackpackItem(Vector2I cell)
	{
		if (!_hasDraggedBackpackItem)
		{
			return false;
		}

		if (cell.X < 0 || cell.Y < 0
			|| cell.X + _draggedBackpackItem.Size.X > TeamBackpackMaxWidth
			|| cell.Y + _draggedBackpackItem.Size.Y > TeamBackpackMaxRows)
		{
			return false;
		}

		if (!IsBackpackAreaEnabled(cell, _draggedBackpackItem.Size) || !IsBackpackAreaFree(cell, _draggedBackpackItem.Size))
		{
			return false;
		}

		_draggedBackpackItem.Cell = cell;
		_runBackpack.Add(_draggedBackpackItem);
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private void RevertDraggedBackpackItem()
	{
		if (!_hasDraggedBackpackItem)
		{
			return;
		}

		_draggedBackpackItem.Cell = _draggedBackpackOriginalCell;
		_runBackpack.Add(_draggedBackpackItem);
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
	}

	private Rect2 GetBackpackItemRect(BackpackItem item, Vector2 gridOrigin, Vector2 cellSize)
	{
		return new Rect2(
			gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
			new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
	}

	private int GetBackpackItemIndexAtPoint(Vector2 point, Vector2 gridOrigin, Vector2 cellSize)
	{
		for (int i = _runBackpack.Count - 1; i >= 0; i--)
		{
			BackpackItem item = _runBackpack[i];
			Rect2 itemRect = GetBackpackItemRect(item, gridOrigin, cellSize);
			if (itemRect.HasPoint(point))
			{
				return i;
			}
		}

		return -1;
	}

	private Vector2I GetBackpackCellAtPoint(Vector2 point, Vector2 gridOrigin, Vector2 cellSize)
	{
		Vector2 local = point - gridOrigin;
		return new Vector2I(
			Mathf.FloorToInt(local.X / cellSize.X),
			Mathf.FloorToInt(local.Y / cellSize.Y));
	}

	private Vector2I GetDraggedBackpackTargetCell(Vector2 gridOrigin, Vector2 cellSize)
	{
		Vector2 dragOrigin = GetViewport().GetMousePosition() - _draggedBackpackGrabOffset;
		Vector2 local = dragOrigin - gridOrigin + new Vector2(Ui(1f), Ui(1f));
		return new Vector2I(
			Mathf.FloorToInt(local.X / cellSize.X),
			Mathf.FloorToInt(local.Y / cellSize.Y));
	}

	private void AutoOrganizeBackpack()
	{
		List<BackpackItem> allItems = new();
		foreach (BackpackItem item in _runBackpack)
		{
			allItems.Add(CloneBackpackItem(item));
		}
		foreach (BackpackItem item in _overflowBackpackItems)
		{
			allItems.Add(CloneBackpackItem(item));
		}
		if (_hasDraggedBackpackItem && _draggedBackpackItem != null)
		{
			allItems.Add(CloneBackpackItem(_draggedBackpackItem));
			_hasDraggedBackpackItem = false;
			_draggedBackpackItem = null;
		}

		List<BackpackItem> bestPlaced = new();
		List<BackpackItem> bestOverflow = new(allItems);
		int bestScore = -1;
		List<BackpackItem> candidate = new(allItems);
		List<System.Comparison<BackpackItem>> strategies =
		[
			(a, b) => CompareBackpackItemsByAreaDesc(a, b),
			(a, b) => CompareBackpackItemsByAreaAsc(a, b),
			(a, b) => CompareBackpackItemsByHeightDesc(a, b),
		];

		for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
		{
			List<BackpackItem> ordered = new(candidate);
			ordered.Sort(strategies[strategyIndex]);
			List<BackpackItem> placed = new();
			List<BackpackItem> overflow = new();

			for (int i = 0; i < ordered.Count; i++)
			{
				BackpackItem item = CloneBackpackItem(ordered[i]);
				if (TryPlaceBackpackItemIntoList(item, placed))
				{
					placed.Add(item);
				}
				else
				{
					overflow.Add(item);
				}
			}

			int score = GetPlacedBackpackScore(placed);
			if (score > bestScore || (score == bestScore && overflow.Count < bestOverflow.Count))
			{
				bestScore = score;
				bestPlaced = placed;
				bestOverflow = overflow;
			}
		}

		_runBackpack.Clear();
		_runBackpack.AddRange(bestPlaced);
		_overflowBackpackItems.Clear();
		_overflowBackpackItems.AddRange(bestOverflow);
		RefreshLootValueFromCurrentInventory();
		_status = _overflowBackpackItems.Count > 0 ? "自动整理完成，仍有物品留在待整理区。" : "自动整理完成。";
	}

	private void DropOverflowToGround()
	{
		if (_overflowBackpackItems.Count == 0 || _inHideout || _runEnded)
		{
			return;
		}

		LootContainer discard = GetOrCreateGroundDiscardContainer();
		for (int i = 0; i < _overflowBackpackItems.Count; i++)
		{
			AddBackpackItemToContainer(discard, _overflowBackpackItems[i]);
		}

		_overflowBackpackItems.Clear();
		RefreshLootValueFromCurrentInventory();
		_selectedContainerIndex = _nodes[_playerNodeId].Containers.IndexOf(discard);
		_status = "已将待整理物品丢到当前房间。";
		RefreshStatus();
	}

	private void DropDraggedBackpackItemToGround()
	{
		if (!_hasDraggedBackpackItem || _draggedBackpackItem == null || _inHideout || _runEnded)
		{
			return;
		}

		LootContainer discard = GetOrCreateGroundDiscardContainer();
		AddBackpackItemToContainer(discard, _draggedBackpackItem);
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		RefreshLootValueFromCurrentInventory();
		_selectedContainerIndex = _nodes[_playerNodeId].Containers.IndexOf(discard);
		_status = "已将物品丢到当前房间。";
		RefreshStatus();
	}

	private LootContainer GetOrCreateGroundDiscardContainer()
	{
		MapNode node = _nodes[_playerNodeId];
		RoomUnit hero = FindHeroUnit();
		Vector2 dropPosition = hero != null ? ClampToRoom(hero.Position + new Vector2(22f, 0f)) : GetRoomArenaRect().GetCenter();
		for (int i = 0; i < node.Containers.Count; i++)
		{
			LootContainer existing = node.Containers[i];
			if (existing.Label == "临时弃置" && existing.Position.DistanceTo(dropPosition) <= 36f)
			{
				return existing;
			}
		}

		LootContainer discard = new()
		{
			Label = "临时弃置",
			Kind = ContainerKind.CorpsePile,
			Position = dropPosition,
			Tint = new Color(0.88f, 0.58f, 0.28f, 1f),
			AutoOpenRange = 62f,
			GridSize = new Vector2I(6, 4),
		};
		node.Containers.Add(discard);
		return discard;
	}

	private BackpackItem CloneBackpackItem(BackpackItem item)
	{
		return new BackpackItem
		{
			Label = item.Label,
			Rarity = item.Rarity,
			Size = item.Size,
			Cell = item.Cell,
			AcquiredInRun = item.AcquiredInRun,
		};
	}

	private void RefreshLootValueFromCurrentInventory()
	{
		int total = 0;
		if (_runEnded && !_runFailed)
		{
			for (int i = 0; i < _hideoutLoadout.Count; i++)
			{
				if (_hideoutLoadout[i].AcquiredInRun)
				{
					total += GetItemValue(_hideoutLoadout[i].Label);
				}
			}

			if (_hasDraggedHideoutItem && _draggedHideoutItem != null && _draggedHideoutItem.AcquiredInRun)
			{
				total += GetItemValue(_draggedHideoutItem.Label);
			}
		}
		else
		{
			for (int i = 0; i < _runBackpack.Count; i++)
			{
				if (_runBackpack[i].AcquiredInRun)
				{
					total += GetItemValue(_runBackpack[i].Label);
				}
			}

			for (int i = 0; i < _overflowBackpackItems.Count; i++)
			{
				if (_overflowBackpackItems[i].AcquiredInRun)
				{
					total += GetItemValue(_overflowBackpackItems[i].Label);
				}
			}

			if (_hasDraggedBackpackItem && _draggedBackpackItem != null && _draggedBackpackItem.AcquiredInRun)
			{
				total += GetItemValue(_draggedBackpackItem.Label);
			}
		}

		_lootValue = total;
	}

	private static int CompareBackpackItemsByAreaDesc(BackpackItem a, BackpackItem b)
	{
		int areaCompare = (b.Size.X * b.Size.Y).CompareTo(a.Size.X * a.Size.Y);
		if (areaCompare != 0) return areaCompare;
		return Mathf.Max(b.Size.X, b.Size.Y).CompareTo(Mathf.Max(a.Size.X, a.Size.Y));
	}

	private static int CompareBackpackItemsByAreaAsc(BackpackItem a, BackpackItem b)
	{
		int areaCompare = (a.Size.X * a.Size.Y).CompareTo(b.Size.X * b.Size.Y);
		if (areaCompare != 0) return areaCompare;
		return Mathf.Max(a.Size.X, a.Size.Y).CompareTo(Mathf.Max(b.Size.X, b.Size.Y));
	}

	private static int CompareBackpackItemsByHeightDesc(BackpackItem a, BackpackItem b)
	{
		int heightCompare = b.Size.Y.CompareTo(a.Size.Y);
		if (heightCompare != 0) return heightCompare;
		return (b.Size.X * b.Size.Y).CompareTo(a.Size.X * a.Size.Y);
	}

	private int GetPlacedBackpackScore(List<BackpackItem> items)
	{
		int score = 0;
		foreach (BackpackItem item in items)
		{
			score += item.Size.X * item.Size.Y * 100 + 1;
		}

		return score;
	}

	private bool TryPlaceBackpackItemIntoList(BackpackItem item, List<BackpackItem> placedItems)
	{
		if (TryPlaceBackpackItemIntoListWithSize(item, item.Size, placedItems))
		{
			return true;
		}

		if (item.Size.X != item.Size.Y)
		{
			Vector2I rotated = new(item.Size.Y, item.Size.X);
			if (TryPlaceBackpackItemIntoListWithSize(item, rotated, placedItems))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryPlaceBackpackItemIntoListWithSize(BackpackItem item, Vector2I size, List<BackpackItem> placedItems)
	{
		for (int y = 0; y <= TeamBackpackMaxRows - size.Y; y++)
		{
			for (int x = 0; x <= TeamBackpackMaxWidth - size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsBackpackAreaEnabled(cell, size) || !IsBackpackAreaFreeInList(cell, size, placedItems))
				{
					continue;
				}

				item.Size = size;
				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsBackpackAreaFreeInList(Vector2I cell, Vector2I size, List<BackpackItem> items)
	{
		foreach (BackpackItem existing in items)
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

	private int GetItemValue(string item)
	{
		if (item.Contains("遗物") || item.Contains("徽记") || item.Contains("宝石")) return 18;
		if (item.Contains("锁甲") || item.Contains("军刀") || item.Contains("长枪")) return 12;
		if (item.Contains("包") || item.Contains("口粮")) return 7;
		return 5;
	}

	private ItemRarity GetItemRarityByLabel(string item) => RollGridRarity(item);

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

	private SoldierRecord CloneSoldierRecord(SoldierRecord soldier)
	{
		return new SoldierRecord
		{
			Name = soldier.Name,
			Class = soldier.Class,
			Experience = soldier.Experience,
		};
	}

	private string GetSoldierClassLabel(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Recruit => "新兵",
		SoldierClass.Shield => "盾兵",
		SoldierClass.EliteShield => "精锐盾兵",
		SoldierClass.Pike => "枪兵",
		SoldierClass.Blade => "刀兵",
		SoldierClass.Archer => "弓兵",
		SoldierClass.Cavalry => "骑兵",
		_ => "未知",
	};

	private int GetSoldierPromotionRequirement(SoldierClass targetClass) => targetClass switch
	{
		SoldierClass.Shield => 2,
		SoldierClass.EliteShield => 6,
		SoldierClass.Pike => 2,
		SoldierClass.Blade => 2,
		SoldierClass.Archer => 2,
		SoldierClass.Cavalry => 3,
		_ => 0,
	};

	private int GetSoldierPromotionCost(SoldierClass targetClass) => targetClass switch
	{
		SoldierClass.Shield => 18,
		SoldierClass.EliteShield => 42,
		SoldierClass.Pike => 18,
		SoldierClass.Blade => 18,
		SoldierClass.Archer => 22,
		SoldierClass.Cavalry => 40,
		_ => 0,
	};

	private bool CanPromoteSoldier(SoldierRecord soldier, SoldierClass targetClass)
	{
		bool validTarget = soldier.Class switch
		{
			SoldierClass.Recruit => targetClass is not SoldierClass.Recruit and not SoldierClass.EliteShield,
			SoldierClass.Shield => targetClass == SoldierClass.EliteShield,
			_ => false,
		};

		return validTarget
			&& soldier.Experience >= GetSoldierPromotionRequirement(targetClass)
			&& _money >= GetSoldierPromotionCost(targetClass);
	}

	private Color GetSoldierClassColor(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Recruit => new Color(0.76f, 0.82f, 0.84f),
		SoldierClass.Shield => new Color(0.46f, 0.84f, 0.7f),
		SoldierClass.EliteShield => new Color(0.68f, 0.94f, 0.82f),
		SoldierClass.Pike => new Color(0.88f, 0.8f, 0.48f),
		SoldierClass.Blade => new Color(0.94f, 0.52f, 0.44f),
		SoldierClass.Archer => new Color(0.6f, 0.78f, 0.98f),
		SoldierClass.Cavalry => new Color(0.92f, 0.7f, 0.34f),
		_ => Colors.White,
	};

	private bool IsSoldierRangedClass(SoldierClass soldierClass) => soldierClass == SoldierClass.Archer;

	private SoldierActiveSkill GetSoldierActiveSkill(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Archer => SoldierActiveSkill.None,
		SoldierClass.EliteShield => SoldierActiveSkill.ShieldRush,
		SoldierClass.Recruit or SoldierClass.Pike or SoldierClass.Blade or SoldierClass.Cavalry => SoldierActiveSkill.Sprint,
		_ => SoldierActiveSkill.None,
	};

	private SoldierPassiveSkill GetSoldierPassiveSkill(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Shield => SoldierPassiveSkill.MissileGuard,
		SoldierClass.EliteShield => SoldierPassiveSkill.MissileGuard,
		_ => SoldierPassiveSkill.None,
	};

	private string GetSoldierActiveSkillLabel(SoldierClass soldierClass) => GetSoldierActiveSkill(soldierClass) switch
	{
		SoldierActiveSkill.Sprint => "主动：跑动",
		SoldierActiveSkill.ShieldRush => "主动：盾冲（耗体力，5秒冷却）",
		_ => "主动：无",
	};

	private string GetSoldierPassiveSkillLabel(SoldierClass soldierClass) => GetSoldierPassiveSkill(soldierClass) switch
	{
		SoldierPassiveSkill.MissileGuard => "被动：对远程攻击减伤 50%",
		_ => "被动：无",
	};

	private int GetSoldierStrengthValue(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Recruit => 1,
		SoldierClass.Shield => 2,
		SoldierClass.EliteShield => 3,
		SoldierClass.Pike => 2,
		SoldierClass.Blade => 2,
		SoldierClass.Archer => 2,
		SoldierClass.Cavalry => 3,
		_ => 1,
	};

	private void RecalculatePlayerStrength()
	{
		int total = 3;
		for (int i = 0; i < _runSoldiers.Count; i++)
		{
			total += GetSoldierStrengthValue(_runSoldiers[i].Class);
		}

		_playerStrength = total;
	}

	private void ApplySoldierClassToRoomUnit(RoomUnit unit, SoldierRecord soldier)
	{
		unit.SoldierClass = soldier.Class;
		unit.IsRanged = IsSoldierRangedClass(soldier.Class);
		unit.Name = $"{soldier.Name}·{GetSoldierClassLabel(soldier.Class)}";
		unit.Armor = 0;
		unit.CanSprint = !unit.IsRanged;
		unit.ActiveSkill = unit.CanSprint ? SoldierActiveSkill.Sprint : SoldierActiveSkill.None;
		unit.PassiveSkill = SoldierPassiveSkill.None;
		unit.ProjectileDamageScale = 1f;

		switch (soldier.Class)
		{
			case SoldierClass.Shield:
				unit.Hp = 15;
				unit.MaxHp = 15;
				unit.DamageMin = 1;
				unit.DamageMax = 2;
				unit.Armor = 2;
				unit.AttackRange = 26f;
				unit.Speed = 122f;
				unit.MaxStamina = 86f;
				unit.Stamina = 86f;
				unit.CanSprint = false;
				unit.PassiveSkill = SoldierPassiveSkill.MissileGuard;
				unit.ProjectileDamageScale = 0.5f;
				break;
			case SoldierClass.EliteShield:
				unit.Hp = 20;
				unit.MaxHp = 20;
				unit.DamageMin = 2;
				unit.DamageMax = 4;
				unit.Armor = 3;
				unit.AttackRange = 28f;
				unit.Speed = 132f;
				unit.MaxStamina = 104f;
				unit.Stamina = 104f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.ShieldRush;
				unit.PassiveSkill = SoldierPassiveSkill.MissileGuard;
				unit.ProjectileDamageScale = 0.5f;
				unit.AttackCycleScale = 0.9f;
				break;
			case SoldierClass.Pike:
				unit.Hp = 9;
				unit.MaxHp = 9;
				unit.DamageMin = 2;
				unit.DamageMax = 4;
				unit.AttackRange = 44f;
				unit.Speed = 146f;
				unit.MaxStamina = 76f;
				unit.Stamina = 76f;
				break;
			case SoldierClass.Blade:
				unit.Hp = 8;
				unit.MaxHp = 8;
				unit.DamageMin = 2;
				unit.DamageMax = 5;
				unit.AttackRange = 28f;
				unit.Speed = 164f;
				unit.MaxStamina = 78f;
				unit.Stamina = 78f;
				break;
			case SoldierClass.Archer:
				unit.Hp = 7;
				unit.MaxHp = 7;
				unit.DamageMin = 1;
				unit.DamageMax = 4;
				unit.AttackRange = 176f;
				unit.Speed = 148f;
				unit.MaxStamina = 0f;
				unit.Stamina = 0f;
				unit.ActiveSkill = SoldierActiveSkill.None;
				break;
			case SoldierClass.Cavalry:
				unit.Hp = 11;
				unit.MaxHp = 11;
				unit.DamageMin = 3;
				unit.DamageMax = 6;
				unit.AttackRange = 34f;
				unit.Speed = 176f;
				unit.AttackCycleScale = 0.92f;
				unit.MaxStamina = 104f;
				unit.Stamina = 104f;
				break;
			default:
				unit.Hp = 8;
				unit.MaxHp = 8;
				unit.DamageMin = 1;
				unit.DamageMax = 3;
				unit.AttackRange = 28f;
				unit.Speed = 152f;
				unit.MaxStamina = 72f;
				unit.Stamina = 72f;
				break;
		}
	}

	private void SellStashItem(int index)
	{
		if (index < 0 || index >= _stash.Count)
		{
			return;
		}

		string item = _stash[index].Label;
		_money += GetItemValue(item);
		_stash.RemoveAt(index);
		_selectedStashIndex = -1;
		_selectedShopIndex = -1;
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
		if (!TryAddToStash(entry.Label))
		{
			_money += entry.Price;
			_status = "仓库空间不足，无法买入。";
			return;
		}

		_selectedShopIndex = -1;
		_status = $"已购入 {entry.Label}。";
	}

	private void MoveAllSettlementItemsToStash()
	{
		if (_hideoutLoadout.Count == 0)
		{
			_status = "局外背包当前没有可转移的物品。";
			return;
		}

		List<BackpackItem> movedItems = new();
		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			movedItems.Add(CloneBackpackItem(_hideoutLoadout[i]));
		}

		if (!CanFitItemsInStash(movedItems))
		{
			_status = "仓库空间不足，无法全部转入。";
			return;
		}

		for (int i = 0; i < movedItems.Count; i++)
		{
			TryAddToStash(movedItems[i]);
		}

		_hideoutLoadout.Clear();
		_selectedSettlementIndex = -1;
		RefreshLootValueFromCurrentInventory();
		_status = "已将局外背包全部转入仓库。";
	}

	private void RepackHideoutLoadout()
	{
		List<BackpackItem> items = new();
		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			items.Add(CloneBackpackItem(_hideoutLoadout[i]));
		}
		RebuildHideoutLoadout(items);
	}

	private void RecruitSoldierInternal()
	{
		_soldierRoster.Add(new SoldierRecord
		{
			Name = $"士兵{_nextSoldierId}",
			Class = SoldierClass.Recruit,
			Experience = EnableSoldierBonusStartingXp ? 10 : 0,
		});
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
		_soldierRosterPage = Mathf.Max(0, (_soldierRoster.Count - 1) / 3);
		_selectedSoldierIndex = _soldierRoster.Count - 1;
		_status = $"已征募新兵，当前士兵数：{_soldierRoster.Count}。";
	}

	private void PromoteSelectedSoldier(SoldierClass targetClass)
	{
		if (_selectedSoldierIndex < 0 || _selectedSoldierIndex >= _soldierRoster.Count)
		{
			return;
		}

		SoldierRecord soldier = _soldierRoster[_selectedSoldierIndex];
		if (!CanPromoteSoldier(soldier, targetClass))
		{
			return;
		}

		_money -= GetSoldierPromotionCost(targetClass);
		soldier.Class = targetClass;
		_status = $"{soldier.Name} 已升阶为{GetSoldierClassLabel(targetClass)}。";
	}

	private void GrantRunSoldierExperience(int amount, string reason)
	{
		if (amount <= 0 || _runSoldiers.Count == 0)
		{
			return;
		}

		for (int i = 0; i < _runSoldiers.Count; i++)
		{
			_runSoldiers[i].Experience += amount;
		}

		LogEvent($"幸存士兵因{reason}获得了 {amount} 点经验。");
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

		RecalculatePlayerStrength();
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

	private void DrawHideout()
	{
		GetHideoutLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 shopRect, out Rect2 loadoutRect);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.96f), true);
		DrawRect(panel, Colors.White, false, 2f);

		float x = panel.Position.X + Ui(24f);
		float y = panel.Position.Y + Ui(40f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "局外整备", HorizontalAlignment.Left, -1f, 26, Colors.White);
		y += Ui(38f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"资金：{_money}", HorizontalAlignment.Left, -1f, UiFont(20), new Color(0.95f, 0.86f, 0.48f));
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"可用士兵：{_soldierRoster.Count}", HorizontalAlignment.Left, -1f, UiFont(18), new Color(0.76f, 0.9f, 0.82f));
		y += Ui(30f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "行动地图", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		Rect2 mapPrevRect = new(new Vector2(x + Ui(122f), y - Ui(22f)), new Vector2(Ui(34f), Ui(28f)));
		Rect2 mapNextRect = new(new Vector2(x + Ui(440f), y - Ui(22f)), new Vector2(Ui(34f), Ui(28f)));
		Rect2 mapNameRect = new(new Vector2(x + Ui(166f), y - Ui(22f)), new Vector2(Ui(262f), Ui(28f)));
		DrawButton(mapPrevRect, "<", new Color(0.22f, 0.24f, 0.29f));
		DrawRect(mapNameRect, new Color(0.11f, 0.12f, 0.15f), true);
		DrawRect(mapNameRect, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawString(ThemeDB.FallbackFont, mapNameRect.Position + new Vector2(10f, 17f), GetSelectedMapName(), HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.88f, 0.9f, 0.95f));
		DrawButton(mapNextRect, ">", new Color(0.22f, 0.24f, 0.29f));
		_buttons.Add(new ButtonDef(mapPrevRect, "select_map_prev"));
		_buttons.Add(new ButtonDef(mapNextRect, "select_map_next"));
		y += Ui(38f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"地图定位：{GetSelectedMapRoleLabel()}", HorizontalAlignment.Left, -1f, 14, new Color(0.76f, 0.84f, 0.94f));
		y += Ui(32f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "行动难度", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		Rect2 diffPrevRect = new(new Vector2(x + Ui(122f), y - Ui(22f)), new Vector2(Ui(34f), Ui(28f)));
		Rect2 diffNextRect = new(new Vector2(x + Ui(440f), y - Ui(22f)), new Vector2(Ui(34f), Ui(28f)));
		Rect2 diffNameRect = new(new Vector2(x + Ui(166f), y - Ui(22f)), new Vector2(Ui(262f), Ui(28f)));
		DrawButton(diffPrevRect, "<", new Color(0.22f, 0.24f, 0.29f));
		DrawRect(diffNameRect, new Color(0.11f, 0.12f, 0.15f), true);
		DrawRect(diffNameRect, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawString(ThemeDB.FallbackFont, diffNameRect.Position + new Vector2(10f, 17f), GetDifficultyName(_selectedDifficulty), HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.96f, 0.9f, 0.78f));
		DrawButton(diffNextRect, ">", new Color(0.22f, 0.24f, 0.29f));
		_buttons.Add(new ButtonDef(diffPrevRect, "select_diff_prev"));
		_buttons.Add(new ButtonDef(diffNextRect, "select_diff_next"));
		y += Ui(34f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"武备要求：{GetDifficultyRequirement(_selectedDifficulty)}", HorizontalAlignment.Left, -1f, 14, new Color(0.95f, 0.86f, 0.48f));

		Rect2 startRect = new(new Vector2(panel.End.X - Ui(184f), panel.Position.Y + Ui(26f)), new Vector2(Ui(152f), Ui(38f)));
		DrawButton(startRect, "入局", new Color(0.24f, 0.62f, 0.36f));
		_buttons.Add(new ButtonDef(startRect, "start_run"));

		Rect2 recruitRect = new(new Vector2(panel.End.X - Ui(184f), panel.Position.Y + Ui(74f)), new Vector2(Ui(152f), Ui(34f)));
		DrawButton(recruitRect, $"征募 {RecruitCost}", _money >= RecruitCost ? new Color(0.48f, 0.34f, 0.18f) : new Color(0.24f, 0.24f, 0.28f));
		if (_money >= RecruitCost)
		{
			_buttons.Add(new ButtonDef(recruitRect, "recruit_soldier"));
		}

		Rect2 soldierRect = new(new Vector2(startRect.Position.X - Ui(304f), panel.Position.Y + Ui(28f)), new Vector2(Ui(288f), Ui(190f)));
		DrawRect(soldierRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(soldierRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, soldierRect.Position + new Vector2(Ui(12f), Ui(22f)), "士兵 roster", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		const int SoldierPageSize = 4;
		int soldierPageCount = Mathf.Max(1, (_soldierRoster.Count + SoldierPageSize - 1) / SoldierPageSize);
		_soldierRosterPage = Mathf.Clamp(_soldierRosterPage, 0, soldierPageCount - 1);
		Rect2 soldierPrevRect = new(new Vector2(soldierRect.End.X - Ui(62f), soldierRect.Position.Y + Ui(8f)), new Vector2(Ui(22f), Ui(20f)));
		Rect2 soldierNextRect = new(new Vector2(soldierRect.End.X - Ui(34f), soldierRect.Position.Y + Ui(8f)), new Vector2(Ui(22f), Ui(20f)));
		DrawButton(soldierPrevRect, "<", new Color(0.22f, 0.24f, 0.29f));
		DrawButton(soldierNextRect, ">", new Color(0.22f, 0.24f, 0.29f));
		if (_soldierRosterPage > 0)
		{
			_buttons.Add(new ButtonDef(soldierPrevRect, "soldier_page_prev"));
		}
		if (_soldierRosterPage < soldierPageCount - 1)
		{
			_buttons.Add(new ButtonDef(soldierNextRect, "soldier_page_next"));
		}
		DrawString(ThemeDB.FallbackFont, soldierRect.Position + new Vector2(Ui(186f), Ui(22f)), $"{_soldierRosterPage + 1}/{soldierPageCount}", HorizontalAlignment.Left, Ui(42f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
		float soldierListY = soldierRect.Position.Y + Ui(34f);
		int soldierStartIndex = _soldierRosterPage * SoldierPageSize;
		int visibleSoldierCount = Mathf.Min(_soldierRoster.Count - soldierStartIndex, SoldierPageSize);
		for (int rowIndex = 0; rowIndex < visibleSoldierCount; rowIndex++)
		{
			int soldierIndex = soldierStartIndex + rowIndex;
			SoldierRecord soldier = _soldierRoster[soldierIndex];
			Rect2 row = new(new Vector2(soldierRect.Position.X + Ui(10f), soldierListY), new Vector2(soldierRect.Size.X - Ui(20f), Ui(20f)));
			DrawRect(row, new Color(0.12f, 0.13f, 0.16f), true);
			DrawRect(row, soldierIndex == _selectedSoldierIndex ? new Color(0.95f, 0.86f, 0.48f, 0.95f) : new Color(0.28f, 0.31f, 0.36f), false, soldierIndex == _selectedSoldierIndex ? 2f : 1f);
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(Ui(6f), Ui(14f)), soldier.Name, HorizontalAlignment.Left, Ui(112f), UiFont(11), Colors.White);
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(Ui(122f), Ui(14f)), GetSoldierClassLabel(soldier.Class), HorizontalAlignment.Left, Ui(58f), UiFont(11), GetSoldierClassColor(soldier.Class));
			DrawString(ThemeDB.FallbackFont, row.Position + new Vector2(Ui(184f), Ui(14f)), $"XP {soldier.Experience}", HorizontalAlignment.Left, Ui(56f), UiFont(10), new Color(0.82f, 0.88f, 0.94f));
			_buttons.Add(new ButtonDef(row, "select_soldier", soldierIndex));
			soldierListY += Ui(23f);
		}
		if (_soldierRoster.Count > visibleSoldierCount && _selectedSoldierIndex < 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X + Ui(12f), soldierRect.End.Y - Ui(12f)), $"总数 {_soldierRoster.Count}，可翻页查看其余成员", HorizontalAlignment.Left, soldierRect.Size.X - Ui(24f), UiFont(10), new Color(0.72f, 0.76f, 0.82f));
		}
		if (_selectedSoldierIndex >= 0 && _selectedSoldierIndex < _soldierRoster.Count)
		{
			SoldierRecord selectedSoldier = _soldierRoster[_selectedSoldierIndex];
			float actionY = soldierRect.Position.Y + Ui(132f);
			DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY), $"选中：{selectedSoldier.Name}  {GetSoldierClassLabel(selectedSoldier.Class)}  XP {selectedSoldier.Experience}", HorizontalAlignment.Left, Ui(320f), UiFont(12), Colors.White);
			DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY + Ui(16f)), GetSoldierActiveSkillLabel(selectedSoldier.Class), HorizontalAlignment.Left, Ui(320f), UiFont(10), new Color(0.86f, 0.9f, 0.96f));
			DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY + Ui(31f)), GetSoldierPassiveSkillLabel(selectedSoldier.Class), HorizontalAlignment.Left, Ui(320f), UiFont(10), new Color(0.86f, 0.9f, 0.96f));
			if (selectedSoldier.Class == SoldierClass.Recruit)
			{
				DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY + Ui(47f)), "基础升阶需求 XP 2 / 18 金。骑兵需求 XP 3 / 40 金。", HorizontalAlignment.Left, Ui(320f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
				Rect2 shieldRect = new(new Vector2(soldierRect.Position.X, actionY + Ui(67f)), new Vector2(Ui(54f), Ui(24f)));
				Rect2 pikeRect = new(new Vector2(soldierRect.Position.X + Ui(60f), actionY + Ui(67f)), new Vector2(Ui(54f), Ui(24f)));
				Rect2 bladeRect = new(new Vector2(soldierRect.Position.X + Ui(120f), actionY + Ui(67f)), new Vector2(Ui(54f), Ui(24f)));
				Rect2 archerRect = new(new Vector2(soldierRect.Position.X + Ui(180f), actionY + Ui(67f)), new Vector2(Ui(54f), Ui(24f)));
				Rect2 cavalryRect = new(new Vector2(soldierRect.Position.X + Ui(240f), actionY + Ui(67f)), new Vector2(Ui(54f), Ui(24f)));
				DrawPromotionButton(shieldRect, "盾", selectedSoldier, SoldierClass.Shield, "promote_shield");
				DrawPromotionButton(pikeRect, "枪", selectedSoldier, SoldierClass.Pike, "promote_pike");
				DrawPromotionButton(bladeRect, "刀", selectedSoldier, SoldierClass.Blade, "promote_blade");
				DrawPromotionButton(archerRect, "弓", selectedSoldier, SoldierClass.Archer, "promote_archer");
				DrawPromotionButton(cavalryRect, "骑", selectedSoldier, SoldierClass.Cavalry, "promote_cavalry");
			}
			else if (selectedSoldier.Class == SoldierClass.Shield)
			{
				DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY + Ui(47f)), "精锐盾兵需求 XP 6 / 42 金。获得盾冲与全面强化。", HorizontalAlignment.Left, Ui(320f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
				Rect2 eliteShieldRect = new(new Vector2(soldierRect.Position.X, actionY + Ui(67f)), new Vector2(Ui(96f), Ui(24f)));
				DrawPromotionButton(eliteShieldRect, "精盾", selectedSoldier, SoldierClass.EliteShield, "promote_elite_shield");
			}
		}

		DrawRect(stashRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(shopRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(stashRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawRect(shopRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, stashRect.Position + new Vector2(14f, 24f), "仓库", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);
		DrawString(ThemeDB.FallbackFont, shopRect.Position + new Vector2(14f, 24f), "商店", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);

		Vector2 stashCellSize = new(Ui(22f), Ui(22f));
		Vector2 stashGridOrigin = stashRect.Position + new Vector2(Ui(14f), Ui(46f));
		Vector2 stashGridSize = new(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y);
		Rect2 stashGridRect = new(stashGridOrigin, stashGridSize);
		DrawRect(stashGridRect, new Color(0.07f, 0.08f, 0.1f), true);
		for (int yCell = 0; yCell < StashGridHeight; yCell++)
		{
			for (int xCell = 0; xCell < StashGridWidth; xCell++)
			{
				Rect2 cellRect = new(
					stashGridOrigin + new Vector2(xCell * stashCellSize.X, yCell * stashCellSize.Y),
					stashCellSize - new Vector2(Ui(2f), Ui(2f)));
				DrawRect(cellRect, new Color(0.11f, 0.12f, 0.15f), true);
				DrawRect(cellRect, new Color(0.24f, 0.27f, 0.32f), false, 1f);
			}
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		string stashHoverLabel = "";
		for (int i = 0; i < _stash.Count; i++)
		{
			BackpackItem item = _stash[i];
			Rect2 itemRect = new(
				stashGridOrigin + new Vector2(item.Cell.X * stashCellSize.X, item.Cell.Y * stashCellSize.Y),
				new Vector2(item.Size.X * stashCellSize.X - Ui(2f), item.Size.Y * stashCellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, i == _selectedStashIndex ? new Color(1f, 0.94f, 0.62f, 0.96f) : new Color(1f, 1f, 1f, 0.8f), false, i == _selectedStashIndex ? 2f : 1f);
			if (itemRect.Size.X >= Ui(42f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}

			if (itemRect.HasPoint(mouse))
			{
				stashHoverLabel = item.Label;
			}

			_buttons.Add(new ButtonDef(itemRect, "select_stash", i));
		}

		float stashInfoY = stashGridRect.End.Y + Ui(12f);
		if (_stash.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(stashRect.Position.X + Ui(14f), stashInfoY), "Stash is empty.", HorizontalAlignment.Left, -1f, UiFont(13), new Color(0.72f, 0.76f, 0.82f));
		}
		else if (_selectedStashIndex >= 0 && _selectedStashIndex < _stash.Count)
		{
			BackpackItem selectedItem = _stash[_selectedStashIndex];
			DrawString(ThemeDB.FallbackFont, new Vector2(stashRect.Position.X + Ui(14f), stashInfoY), selectedItem.Label, HorizontalAlignment.Left, stashRect.Size.X - Ui(120f), UiFont(13), Colors.White);
			DrawString(ThemeDB.FallbackFont, new Vector2(stashRect.Position.X + Ui(14f), stashInfoY + Ui(18f)), $"Sell value {GetItemValue(selectedItem.Label)}   Size {selectedItem.Size.X}x{selectedItem.Size.Y}", HorizontalAlignment.Left, stashRect.Size.X - Ui(120f), UiFont(12), new Color(0.95f, 0.86f, 0.48f));
			Rect2 sellRect = new(new Vector2(stashRect.End.X - Ui(92f), stashInfoY - Ui(4f)), new Vector2(Ui(76f), Ui(28f)));
			DrawButton(sellRect, "Sell", new Color(0.46f, 0.25f, 0.19f));
			_buttons.Add(new ButtonDef(sellRect, "sell_stash", _selectedStashIndex));
		}

		if (!string.IsNullOrEmpty(stashHoverLabel))
		{
			DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f)), stashHoverLabel);
		}
		Vector2 shopCellSize = new(Ui(18f), Ui(18f));
		float shopY = shopRect.Position.Y + Ui(44f);
		for (int i = 0; i < _shopStock.Count; i++)
		{
			ShopEntry entry = _shopStock[i];
			Vector2I itemSize = GetBackpackItemSize(entry.Label);
			Rect2 row = new(new Vector2(shopRect.Position.X + Ui(14f), shopY), new Vector2(shopRect.Size.X - Ui(28f), Ui(40f)));
			DrawRect(row, new Color(0.12f, 0.13f, 0.16f), true);
			DrawRect(row, i == _selectedShopIndex ? new Color(0.92f, 0.82f, 0.48f, 0.95f) : new Color(0.34f, 0.37f, 0.42f), false, i == _selectedShopIndex ? 2f : 1f);

			Rect2 previewRect = new(row.Position + new Vector2(Ui(6f), Ui(4f)), new Vector2(itemSize.X * shopCellSize.X - Ui(2f), itemSize.Y * shopCellSize.Y - Ui(2f)));
			Color previewColor = GetGridRarityColor(GetItemRarityByLabel(entry.Label));
			DrawRect(previewRect, previewColor, true);
			DrawRect(previewRect, new Color(1f, 1f, 1f, 0.82f), false, 1f);

			float textX = previewRect.End.X + Ui(10f);
			DrawString(ThemeDB.FallbackFont, new Vector2(textX, row.Position.Y + Ui(17f)), entry.Label, HorizontalAlignment.Left, row.Size.X - Ui(170f), UiFont(12), Colors.White);
			DrawString(ThemeDB.FallbackFont, new Vector2(textX, row.Position.Y + Ui(33f)), $"Price {entry.Price}   Size {itemSize.X}x{itemSize.Y}", HorizontalAlignment.Left, row.Size.X - Ui(170f), UiFont(11), new Color(0.78f, 0.87f, 0.98f));

			Rect2 selectRect = new(row.Position, new Vector2(row.Size.X - Ui(74f), row.Size.Y));
			_buttons.Add(new ButtonDef(selectRect, "select_shop", i));

			bool canAfford = _money >= entry.Price;
			Rect2 buyRect = new(new Vector2(row.End.X - Ui(64f), row.Position.Y + Ui(6f)), new Vector2(Ui(56f), Ui(28f)));
			DrawButton(buyRect, canAfford ? "Buy" : "Low", canAfford ? new Color(0.26f, 0.42f, 0.58f) : new Color(0.24f, 0.24f, 0.28f));
			if (canAfford)
			{
				_buttons.Add(new ButtonDef(buyRect, "buy_shop", i));
			}

			shopY += Ui(46f);
		}

		if (_selectedShopIndex >= 0 && _selectedShopIndex < _shopStock.Count)
		{
			ShopEntry selectedShop = _shopStock[_selectedShopIndex];
			Vector2I selectedSize = GetBackpackItemSize(selectedShop.Label);
			float infoY = shopRect.End.Y - Ui(42f);
			DrawString(ThemeDB.FallbackFont, new Vector2(shopRect.Position.X + Ui(14f), infoY), selectedShop.Label, HorizontalAlignment.Left, shopRect.Size.X - Ui(120f), UiFont(13), Colors.White);
			DrawString(ThemeDB.FallbackFont, new Vector2(shopRect.Position.X + Ui(14f), infoY + Ui(18f)), $"Price {selectedShop.Price}   Size {selectedSize.X}x{selectedSize.Y}", HorizontalAlignment.Left, shopRect.Size.X - Ui(120f), UiFont(12), new Color(0.78f, 0.87f, 0.98f));
		}

		DrawRect(loadoutRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(loadoutRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, loadoutRect.Position + new Vector2(Ui(14f), Ui(24f)), "预备携行", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		DrawString(ThemeDB.FallbackFont, loadoutRect.Position + new Vector2(Ui(118f), Ui(24f)), "入局时这些物品会直接进入队伍背包。", HorizontalAlignment.Left, loadoutRect.Size.X - Ui(220f), UiFont(12), new Color(0.82f, 0.88f, 0.94f));
		Rect2 autoPackRect = new(new Vector2(loadoutRect.End.X - Ui(108f), loadoutRect.Position.Y + Ui(12f)), new Vector2(Ui(92f), Ui(28f)));
		DrawButton(autoPackRect, "整理背包", new Color(0.28f, 0.42f, 0.26f));
		_buttons.Add(new ButtonDef(autoPackRect, "auto_pack_hideout"));

		Vector2 loadoutCellSize = new(Ui(18f), Ui(18f));
		Vector2 loadoutGridOrigin = loadoutRect.Position + new Vector2(Ui(14f), Ui(42f));
		List<BackpackCapacityBlock> loadoutBlocks = BuildHideoutLoadoutCapacityBlocks();
		for (int i = 0; i < loadoutBlocks.Count; i++)
		{
			BackpackCapacityBlock block = loadoutBlocks[i];
			Rect2 blockRect = new(
				loadoutGridOrigin + new Vector2(block.Cell.X * loadoutCellSize.X, block.Cell.Y * loadoutCellSize.Y),
				new Vector2(block.Size.X * loadoutCellSize.X - Ui(2f), block.Size.Y * loadoutCellSize.Y - Ui(2f)));
			DrawRect(blockRect, i == 0 ? new Color(0.18f, 0.2f, 0.26f, 0.65f) : new Color(0.16f, 0.18f, 0.22f, 0.6f), true);
			DrawDashedRect(blockRect, new Color(0.82f, 0.86f, 0.94f, 0.4f));
			for (int by = 0; by < block.Size.Y; by++)
			{
				for (int bx = 0; bx < block.Size.X; bx++)
				{
					Rect2 cellRect = new(
						loadoutGridOrigin + new Vector2((block.Cell.X + bx) * loadoutCellSize.X, (block.Cell.Y + by) * loadoutCellSize.Y),
						loadoutCellSize - new Vector2(Ui(2f), Ui(2f)));
					DrawRect(cellRect, new Color(0.08f, 0.09f, 0.11f), true);
					DrawRect(cellRect, new Color(0.2f, 0.23f, 0.28f), false, 1f);
				}
			}
		}

		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			BackpackItem item = _hideoutLoadout[i];
			Rect2 itemRect = new(
				loadoutGridOrigin + new Vector2(item.Cell.X * loadoutCellSize.X, item.Cell.Y * loadoutCellSize.Y),
				new Vector2(item.Size.X * loadoutCellSize.X - Ui(2f), item.Size.Y * loadoutCellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, i == _selectedLoadoutIndex ? new Color(1f, 0.94f, 0.62f, 0.96f) : new Color(1f, 1f, 1f, 0.8f), false, i == _selectedLoadoutIndex ? 2f : 1f);
			if (itemRect.Size.X >= Ui(42f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}
			if (itemRect.HasPoint(mouse))
			{
				stashHoverLabel = item.Label;
			}
			_buttons.Add(new ButtonDef(itemRect, "select_loadout", i));
		}

		float loadoutInfoX = loadoutGridOrigin.X + Ui(250f);
		float loadoutInfoY = loadoutRect.Position.Y + Ui(48f);
		DrawString(ThemeDB.FallbackFont, new Vector2(loadoutInfoX, loadoutInfoY), $"士兵数 {_soldierRoster.Count}   载具块 {loadoutBlocks.Count}", HorizontalAlignment.Left, loadoutRect.Size.X - Ui(280f), UiFont(12), new Color(0.78f, 0.87f, 0.98f));
		if (_selectedStashIndex >= 0 && _selectedStashIndex < _stash.Count)
		{
			BackpackItem selectedStash = _stash[_selectedStashIndex];
			DrawString(ThemeDB.FallbackFont, new Vector2(loadoutInfoX, loadoutInfoY + Ui(22f)), $"仓库选中：{selectedStash.Label}  {selectedStash.Size.X}x{selectedStash.Size.Y}", HorizontalAlignment.Left, loadoutRect.Size.X - Ui(280f), UiFont(12), Colors.White);
			DrawString(ThemeDB.FallbackFont, new Vector2(loadoutInfoX, loadoutInfoY + Ui(42f)), "再次点击同一物品即可拿起，然后放到右侧网格。", HorizontalAlignment.Left, loadoutRect.Size.X - Ui(280f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
		}
		else if (_selectedLoadoutIndex >= 0 && _selectedLoadoutIndex < _hideoutLoadout.Count)
		{
			BackpackItem selectedLoadout = _hideoutLoadout[_selectedLoadoutIndex];
			DrawString(ThemeDB.FallbackFont, new Vector2(loadoutInfoX, loadoutInfoY + Ui(22f)), $"预备携行选中：{selectedLoadout.Label}  {selectedLoadout.Size.X}x{selectedLoadout.Size.Y}", HorizontalAlignment.Left, loadoutRect.Size.X - Ui(280f), UiFont(12), Colors.White);
			DrawString(ThemeDB.FallbackFont, new Vector2(loadoutInfoX, loadoutInfoY + Ui(42f)), "再次点击同一物品即可拿起，然后放回左侧仓库网格。", HorizontalAlignment.Left, loadoutRect.Size.X - Ui(280f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
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
				DrawMapPath(MapToScreen(node.Position), MapToScreen(_nodes[link].Position), highlight);
			}
		}

		float mapScale = GetMapUnitScale();
		foreach (MapNode node in _nodes)
		{
			Vector2 nodePos = MapToScreen(node.Position);
			if (!node.Revealed)
			{
				DrawCircle(nodePos, 10f * mapScale, new Color(0.18f, 0.17f, 0.16f, 0.45f));
				DrawString(ThemeDB.FallbackFont, nodePos + new Vector2(-6f, -12f) * mapScale, "?", HorizontalAlignment.Left, -1f, UiFont(12), new Color(0.68f, 0.64f, 0.58f, 0.7f));
				continue;
			}

			bool clearVision = HasClearVision(node);
			Color color = GetNodeColor(node, clearVision);
			float outerAlpha = clearVision ? 0.95f : 0.55f;
			float labelAlpha = clearVision ? 1f : 0.65f;
			DrawCircle(nodePos, 26f * mapScale, new Color(0.14f, 0.12f, 0.1f, outerAlpha));
			DrawCircle(nodePos, 22f * mapScale, color);
			DrawArc(nodePos, 31f * mapScale, 0f, Mathf.Tau, 32, new Color(color.R, color.G, color.B, clearVision ? 0.38f : 0.18f), 2.2f * mapScale);
			DrawNodeGlyph(node, nodePos, color, mapScale);
			Vector2 labelPos = nodePos + GetNodeLabelOffset(node.Id) * mapScale;
			DrawString(ThemeDB.FallbackFont, labelPos, node.Name, HorizontalAlignment.Left, -1f, UiFont(15), new Color(0.96f, 0.93f, 0.86f, labelAlpha));
			if (node.Id == _playerNodeId && !_isPlayerMoving)
			{
				DrawArc(nodePos, 34f * mapScale, 0f, Mathf.Tau, 32, new Color(0.58f, 0.95f, 0.98f, 0.9f), 3f * mapScale);
				DrawCircle(nodePos, 7f * mapScale, new Color(0.58f, 0.95f, 0.98f));
			}
			AiSquad squad = GetSquadAtNode(node.Id);
			if (squad != null && clearVision)
			{
				Vector2 badge = nodePos + new Vector2(20f, 18f) * mapScale;
				DrawCircle(badge, 9f * mapScale, new Color(0.46f, 0.18f, 0.04f, 0.95f));
				DrawCircle(badge, 7f * mapScale, new Color(0.96f, 0.62f, 0.22f));
				DrawString(ThemeDB.FallbackFont, nodePos + GetSquadLabelOffset(node.Id) * mapScale, squad.Name, HorizontalAlignment.Left, -1f, UiFont(11), new Color(1f, 0.9f, 0.72f));
				Vector2 intentPos = nodePos + GetSquadIntentOffset(node.Id) * mapScale;
				DrawAiIntentIcon(intentPos + new Vector2(6f, -3f) * mapScale, squad.Intent);
				DrawString(ThemeDB.FallbackFont, intentPos + new Vector2(18f, 0f), $"现：{GetAiIntentSummary(squad)}", HorizontalAlignment.Left, -1f, UiFont(10), new Color(0.92f, 0.92f, 0.84f));
				DrawString(ThemeDB.FallbackFont, intentPos + new Vector2(18f, 12f), $"后：{GetAiNextActionSummary(squad)}", HorizontalAlignment.Left, -1f, UiFont(10), new Color(0.72f, 0.86f, 0.98f, 0.95f));

				int nextNodeId = GetAiPredictedNextNodeId(squad);
				if (nextNodeId >= 0 && nextNodeId < _nodes.Count && nextNodeId != squad.NodeId)
				{
					DrawPredictedMoveArrow(nodePos, MapToScreen(_nodes[nextNodeId].Position));
				}
			}
		}

		Vector2 marker = MapToScreen(_playerMarkerPosition);
		DrawArc(marker, 34f * mapScale, 0f, Mathf.Tau, 32, new Color(0.58f, 0.95f, 0.98f, 0.9f), 3f * mapScale);
		DrawCircle(marker, 7f * mapScale, new Color(0.58f, 0.95f, 0.98f));
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
		DrawString(ThemeDB.FallbackFont, banner.Position + new Vector2(16f, 26f), $"战术房间: {node.Name}", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);
		DrawString(ThemeDB.FallbackFont, banner.Position + new Vector2(16f, 50f), $"类型 {GetNodeTypeLabel(node.Type)}  威胁 {node.Threat}  搜刮 {CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.76f, 0.82f, 0.9f));
		DrawString(ThemeDB.FallbackFont, banner.Position + new Vector2(16f, 70f), "按 M 打开战略地图，默认操作留在房间层。", HorizontalAlignment.Left, -1f, UiFont(13), new Color(0.94f, 0.84f, 0.62f));

		Rect2 roomCore = new(_mapRect.Position + new Vector2(120f, 136f), _mapRect.Size - new Vector2(240f, 250f));
		DrawRect(roomCore, new Color(0.08f, 0.09f, 0.11f, 0.7f), true);
		DrawRect(roomCore, new Color(0.42f, 0.45f, 0.5f, 0.95f), false, 2f);
		DrawString(ThemeDB.FallbackFont, roomCore.Position + new Vector2(18f, 26f), "房间内部", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);
		DrawString(ThemeDB.FallbackFont, roomCore.Position + new Vector2(18f, 50f), "这里承接搜索、战斗和转场决策。", HorizontalAlignment.Left, -1f, UiFont(13), new Color(0.82f, 0.86f, 0.92f));
		DrawString(ThemeDB.FallbackFont, roomCore.Position + new Vector2(18f, 72f), _plannedExitNodeId >= 0 ? $"已规划出口：{_nodes[_plannedExitNodeId].Name}" : "尚未规划出口，可直接在房间内选出口。", HorizontalAlignment.Left, -1f, UiFont(13), new Color(0.94f, 0.84f, 0.62f));
		DrawRect(new Rect2(roomCore.Position + new Vector2(28f, 96f), new Vector2(roomCore.Size.X - 56f, roomCore.Size.Y - 132f)), new Color(0.16f, 0.18f, 0.2f, 0.45f), false, 2f);

		float exitY = _mapRect.End.Y - 110f;
		DrawString(ThemeDB.FallbackFont, new Vector2(_mapRect.Position.X + 24f, exitY), "房间出口", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		exitY += 24f;
		if (node.Links.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(_mapRect.Position.X + 24f, exitY), "当前房间没有可用出口。", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.76f, 0.8f, 0.86f));
			return;
		}

		for (int i = 0; i < node.Links.Count; i++)
		{
			int linkedNodeId = node.Links[i];
			MapNode linkedNode = _nodes[linkedNodeId];
			string exitLabel = $"{GetExitDirectionLabel(GetExitDirection(node, linkedNodeId))} -> {linkedNode.Name}";
			DrawString(ThemeDB.FallbackFont, new Vector2(_mapRect.Position.X + 24f, exitY), exitLabel, HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.76f, 0.8f, 0.86f));
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
		DrawString(ThemeDB.FallbackFont, titleBar.Position + new Vector2(16f, 24f), "战略地图", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);
		DrawString(ThemeDB.FallbackFont, titleBar.Position + new Vector2(16f, 48f), "点击相邻节点执行转场，再按 M 返回房间视图。", HorizontalAlignment.Left, -1f, UiFont(13), new Color(0.86f, 0.9f, 0.95f));
	}

	private void DrawRoomExits(MapNode node)
	{
		for (int i = 0; i < node.Links.Count; i++)
		{
			int linkedNodeId = node.Links[i];
			MapNode linkedNode = _nodes[linkedNodeId];
			Rect2 exitRect = GetRoomExitRect(node, linkedNodeId);
			bool planned = linkedNodeId == _plannedExitNodeId;
			Color fill = planned ? new Color(0.3f, 0.54f, 0.68f, 0.9f) : new Color(0.18f, 0.2f, 0.24f, 0.92f);
			Color border = planned ? new Color(0.92f, 0.84f, 0.58f, 0.98f) : new Color(0.62f, 0.68f, 0.76f, 0.95f);
			DrawRect(exitRect, fill, true);
			DrawRect(exitRect, border, false, 2f);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(10f, 18f), GetExitDirectionLabel(GetExitDirection(node, linkedNodeId)), HorizontalAlignment.Left, exitRect.Size.X - 20f, UiFont(13), Colors.White);
			DrawString(ThemeDB.FallbackFont, exitRect.Position + new Vector2(10f, 38f), linkedNode.Name, HorizontalAlignment.Left, exitRect.Size.X - 20f, UiFont(13), new Color(0.88f, 0.92f, 0.98f));
			_buttons.Add(new ButtonDef(exitRect, "use_exit", linkedNodeId));
		}
	}

	private string GetExitDirectionLabel(Vector2 direction)
	{
		Vector2 dir = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
		float angle = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X)) + 360f, 360f);
		string horizontal = dir.X switch
		{
			> 0.38f => "E",
			< -0.38f => "W",
			_ => ""
		};
		string vertical = dir.Y switch
		{
			> 0.38f => "S",
			< -0.38f => "N",
			_ => ""
		};
		string label = $"{vertical}{horizontal}";
		return string.IsNullOrEmpty(label) ? $"{Mathf.RoundToInt(angle)}°" : label;
	}

	private string GetCleanExitDirectionLabel(Vector2 direction) => GetExitDirectionLabel(direction);

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


	private void DrawMonasteryBackdrop()
	{
		Rect2 canvas = GetMapCanvasRect();
		DrawRect(canvas, new Color(0.17f, 0.15f, 0.12f), true);
		DrawRect(canvas.GrowIndividual(-Ui(10f), -Ui(10f), -Ui(10f), -Ui(10f)), new Color(0.22f, 0.2f, 0.16f), false, 2f);

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

		DrawLine(MapToScreen(new Vector2(182f, 360f)), MapToScreen(new Vector2(250f, 360f)), new Color(0.46f, 0.4f, 0.31f, 0.8f), 12f * GetMapUnitScale());
		DrawLine(MapToScreen(new Vector2(520f, 360f)), MapToScreen(new Vector2(620f, 360f)), new Color(0.46f, 0.4f, 0.31f, 0.8f), 12f * GetMapUnitScale());
		DrawLine(MapToScreen(new Vector2(620f, 255f)), MapToScreen(new Vector2(620f, 455f)), new Color(0.46f, 0.4f, 0.31f, 0.8f), 12f * GetMapUnitScale());
		DrawLine(MapToScreen(new Vector2(280f, 505f)), MapToScreen(new Vector2(550f, 505f)), new Color(0.46f, 0.4f, 0.31f, 0.8f), 10f * GetMapUnitScale());
	}

	private void DrawBorderKeepBackdrop()
	{
		Rect2 canvas = GetMapCanvasRect();
		DrawRect(canvas, new Color(0.16f, 0.15f, 0.13f), true);
		DrawRect(canvas.GrowIndividual(-Ui(16f), -Ui(16f), -Ui(16f), -Ui(16f)), new Color(0.22f, 0.2f, 0.18f), false, 2f);

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

		DrawLine(MapToScreen(new Vector2(235f, 510f)), MapToScreen(new Vector2(650f, 510f)), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f * GetMapUnitScale());
		DrawLine(MapToScreen(new Vector2(390f, 170f)), MapToScreen(new Vector2(650f, 170f)), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f * GetMapUnitScale());
		DrawLine(MapToScreen(new Vector2(390f, 170f)), MapToScreen(new Vector2(390f, 510f)), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f * GetMapUnitScale());
		DrawLine(MapToScreen(new Vector2(650f, 170f)), MapToScreen(new Vector2(650f, 510f)), new Color(0.44f, 0.39f, 0.31f, 0.8f), 10f * GetMapUnitScale());
	}

	private void DrawDistrictBlock(Rect2 rect, Color fill, string label, Vector2 labelOffset)
	{
		Rect2 scaled = MapToScreen(rect);
		DrawRect(scaled, fill, true);
		DrawRect(scaled, new Color(0.52f, 0.46f, 0.36f, 0.95f), false, 2f * GetMapUnitScale());
	}

	private void DrawMapPath(Vector2 from, Vector2 to, bool highlight)
	{
		Color baseColor = highlight
			? new Color(0.72f, 0.64f, 0.47f, 0.82f)
			: new Color(0.48f, 0.42f, 0.32f, 0.26f);
		float width = (highlight ? 3.2f : 1.6f) * GetMapUnitScale();
		DrawLine(from, to, baseColor, width);
	}

	private void DrawNodeGlyph(MapNode node, Vector2 position, Color color, float scale)
	{
		Vector2 p = position;
		Color ink = new Color(0.09f, 0.08f, 0.07f, 0.9f);
		switch (node.Type)
		{
			case NodeType.Extract:
				DrawLine(p + new Vector2(-7f, 0f) * scale, p + new Vector2(8f, 0f) * scale, ink, 2f * scale);
				DrawLine(p + new Vector2(3f, -5f) * scale, p + new Vector2(8f, 0f) * scale, ink, 2f * scale);
				DrawLine(p + new Vector2(3f, 5f) * scale, p + new Vector2(8f, 0f) * scale, ink, 2f * scale);
				break;
			case NodeType.Battle:
				DrawLine(p + new Vector2(-7f, -7f) * scale, p + new Vector2(7f, 7f) * scale, ink, 2.2f * scale);
				DrawLine(p + new Vector2(-7f, 7f) * scale, p + new Vector2(7f, -7f) * scale, ink, 2.2f * scale);
				break;
			case NodeType.Search:
				DrawCircle(p + new Vector2(-1f, -1f) * scale, 6f * scale, ink);
				DrawLine(p + new Vector2(4f, 4f) * scale, p + new Vector2(10f, 10f) * scale, ink, 2f * scale);
				DrawCircle(p + new Vector2(-1f, -1f) * scale, 3.5f * scale, color);
				break;
			default:
				DrawRect(new Rect2(p + new Vector2(-6f, -6f) * scale, new Vector2(12f, 12f) * scale), ink, false, 2f * scale);
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
		if (node.Threat > 0) return new Color(0.68f, 0.7f, 0.74f);
		if (CountNodeLoot(node) > 0) return new Color(0.75f, 0.62f, 0.24f);
		return node.Visited ? new Color(0.31f, 0.45f, 0.62f) : new Color(0.2f, 0.23f, 0.28f);
	}

	private void DrawSidePanel()
	{
		DrawRect(_sideRect, new Color(0.05f, 0.05f, 0.06f, 0.96f), true);
		DrawRect(_sideRect, Colors.White, false, 2f);
		float x = _sideRect.Position.X + Ui(22f);
		float y = _sideRect.Position.Y + Ui(34f);
		float panelBottom = _sideRect.End.Y - Ui(22f);
		MapNode node = _nodes[_playerNodeId];
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "\u8282\u70b9\u7a81\u88ad Demo", HorizontalAlignment.Left, -1f, UiFont(22), Colors.White);
		y += Ui(34f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u56de\u5408 {_turn}", HorizontalAlignment.Left, -1f, UiFont(18), new Color(0.76f, 0.84f, 0.95f));
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u751f\u547d {_playerHp}/{_playerMaxHp}   \u6218\u529b {_playerStrength}", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		y += Ui(26f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u65f6\u9699 {_timeSlotProgress}/100   \u6218\u5229\u54c1\u4ef7\u503c {_lootValue}", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		y += Ui(24f);
		Rect2 timeBar = new(new Vector2(x, y), new Vector2(_sideRect.Size.X - Ui(44f), Ui(12f)));
		DrawRect(timeBar, new Color(0.12f, 0.13f, 0.16f), true);
		DrawRect(timeBar, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawRect(new Rect2(timeBar.Position, new Vector2(timeBar.Size.X * (_timeSlotProgress / 100f), timeBar.Size.Y)), new Color(0.84f, 0.66f, 0.26f), true);
		y += Ui(38f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, _sideRect.Size.X - Ui(44f), UiFont(15), new Color(0.86f, 0.9f, 0.95f));
		y += Ui(64f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u5f53\u524d\u8282\u70b9\uff1a{node.Name}", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u7c7b\u578b\uff1a{GetNodeTypeLabel(node.Type)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.75f, 0.78f, 0.82f));
		y += Ui(22f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u5a01\u80c1\uff1a{node.Threat}   \u6218\u5229\u54c1\uff1a{CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.75f, 0.78f, 0.82f));
		y += Ui(22f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u62fe\u53d6\u8d44\u91d1\uff1a{_runMoneyLooted}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.95f, 0.86f, 0.48f));
		y += Ui(22f);
		int carryUsage = GetCurrentCarryUsage();
		int carryLimit = GetCurrentCarryLimit();
		bool overloaded = IsOverloaded();
		Color carryColor = overloaded ? new Color(0.96f, 0.42f, 0.4f) : new Color(0.78f, 0.86f, 0.95f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"携行占用：{carryUsage} / {carryLimit}", HorizontalAlignment.Left, -1f, UiFont(14), carryColor);
		y += Ui(20f);
		if (overloaded)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "超载：无法过门或撤离", HorizontalAlignment.Left, -1f, UiFont(13), new Color(0.96f, 0.52f, 0.48f));
			y += Ui(20f);
		}
		float actionWidth = (_sideRect.Size.X - Ui(60f)) * 0.5f;
		Rect2 mapRect = new(new Vector2(x, y), new Vector2(actionWidth, Ui(34f)));
		DrawButton(mapRect, _showMapOverlay ? "战略地图：开" : "战略地图：关", _showMapOverlay ? new Color(0.24f, 0.48f, 0.62f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(mapRect, "toggle_map"));
		Rect2 autoRect = new(new Vector2(x + actionWidth + Ui(16f), y), new Vector2(actionWidth, Ui(34f)));
		DrawButton(autoRect, _autoSearchEnabled ? "\u81ea\u52a8\u641c\u7d22\uff1a\u5f00" : "\u81ea\u52a8\u641c\u7d22\uff1a\u5173", _autoSearchEnabled ? new Color(0.24f, 0.56f, 0.32f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(autoRect, "toggle_auto_search"));
		if (node.Type == NodeType.Extract && !_runEnded)
		{
			Rect2 rect = new(new Vector2(x + actionWidth + Ui(16f), y + Ui(42f)), new Vector2(actionWidth, Ui(34f)));
			DrawButton(rect, "\u6267\u884c\u64a4\u79bb", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(rect, "extract"));
		}
		y += node.Type == NodeType.Extract ? Ui(88f) : Ui(50f);
		float backpackTop = y + Ui(8f);
		float backpackHeight = DrawTeamBackpackPreview(new Vector2(x, backpackTop));
		float logTop = backpackTop + backpackHeight + Ui(26f);
		logTop = Mathf.Max(logTop, panelBottom - Ui(144f));
		DrawLine(new Vector2(x, logTop - 10f), new Vector2(_sideRect.End.X - 18f, logTop - 10f), new Color(0.24f, 0.27f, 0.31f), 1f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logTop), "\u4e16\u754c\u52a8\u6001", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		float logY = logTop + Ui(24f);
		int startIndex = Mathf.Max(0, _eventLog.Count - 5);
		for (int i = startIndex; i < _eventLog.Count; i++)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(x, logY), _eventLog[i], HorizontalAlignment.Left, _sideRect.Size.X - Ui(44f), UiFont(14), new Color(0.8f, 0.84f, 0.9f));
			logY += Ui(22f);
		}
	}

	private void DrawButton(Rect2 rect, string text, Color color)
	{
		DrawRect(rect, color, true);
		DrawRect(rect, Colors.White, false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), rect.Size.Y - Ui(8f)), text, HorizontalAlignment.Left, rect.Size.X - Ui(16f), UiFont(13), Colors.White);
	}

	private void DrawPromotionButton(Rect2 rect, string text, SoldierRecord soldier, SoldierClass targetClass, string action)
	{
		bool canPromote = CanPromoteSoldier(soldier, targetClass);
		DrawButton(rect, text, canPromote ? GetSoldierClassColor(targetClass) : new Color(0.24f, 0.24f, 0.28f));
		if (canPromote)
		{
			_buttons.Add(new ButtonDef(rect, action));
		}
	}

	private void DrawDashedRect(Rect2 rect, Color color)
	{
		float dash = Ui(6f);
		for (float x = rect.Position.X; x < rect.End.X; x += dash * 2f)
		{
			DrawLine(new Vector2(x, rect.Position.Y), new Vector2(Mathf.Min(x + dash, rect.End.X), rect.Position.Y), color, 1f);
			DrawLine(new Vector2(x, rect.End.Y), new Vector2(Mathf.Min(x + dash, rect.End.X), rect.End.Y), color, 1f);
		}

		for (float y = rect.Position.Y; y < rect.End.Y; y += dash * 2f)
		{
			DrawLine(new Vector2(rect.Position.X, y), new Vector2(rect.Position.X, Mathf.Min(y + dash, rect.End.Y)), color, 1f);
			DrawLine(new Vector2(rect.End.X, y), new Vector2(rect.End.X, Mathf.Min(y + dash, rect.End.Y)), color, 1f);
		}
	}

	private void DrawInventoryTooltip(Vector2 position, string label)
	{
		Vector2 size = new(Mathf.Max(Ui(96f), label.Length * Ui(9f)), Ui(24f));
		Rect2 rect = new(position, size);
		DrawRect(rect, new Color(0.04f, 0.05f, 0.06f, 0.96f), true);
		DrawRect(rect, new Color(0.9f, 0.92f, 0.96f, 0.92f), false, 1f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(6f), Ui(16f)), label, HorizontalAlignment.Left, rect.Size.X - Ui(8f), UiFont(11), Colors.White);
	}

	private bool HandleHideoutStoragePress(Vector2 click)
	{
		GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize);
		GetSecondaryStorageGridLayout(out Rect2 secondaryGridRect, out Vector2 secondaryGridOrigin, out Vector2 secondaryCellSize);
		bool clickedStash = stashGridRect.HasPoint(click);
		bool clickedSecondary = secondaryGridRect.HasPoint(click);

		if (!clickedStash && !clickedSecondary)
		{
			return false;
		}

		if (_hasDraggedHideoutItem)
		{
			return true;
		}

		if (clickedStash)
		{
			int itemIndex = GetStorageItemIndexAtPoint(click, stashGridOrigin, stashCellSize, _stash);
			if (itemIndex < 0)
			{
				_selectedStashIndex = -1;
				return true;
			}

			_selectedStashIndex = itemIndex;
			_selectedShopIndex = -1;
			_selectedLoadoutIndex = -1;
			_selectedSettlementIndex = -1;
			_pendingHideoutDrag = true;
			_pendingHideoutDragFromLoadout = false;
			_pendingHideoutDragIndex = itemIndex;
			_pendingHideoutGrabOffset = click - GetStorageItemRect(_stash[itemIndex], stashGridOrigin, stashCellSize).Position;
			return true;
		}

		int secondaryIndex = GetStorageItemIndexAtPoint(click, secondaryGridOrigin, secondaryCellSize, _hideoutLoadout);
		if (secondaryIndex < 0)
		{
			if (_inHideout)
			{
				_selectedLoadoutIndex = -1;
			}
			else
			{
				_selectedSettlementIndex = -1;
			}

			return true;
		}

		_selectedStashIndex = -1;
		_selectedShopIndex = -1;
		if (_inHideout)
		{
			_selectedLoadoutIndex = secondaryIndex;
			_selectedSettlementIndex = -1;
		}
		else
		{
			_selectedSettlementIndex = secondaryIndex;
			_selectedLoadoutIndex = -1;
		}

		_pendingHideoutDrag = true;
		_pendingHideoutDragFromLoadout = true;
		_pendingHideoutDragIndex = secondaryIndex;
		_pendingHideoutGrabOffset = click - GetStorageItemRect(_hideoutLoadout[secondaryIndex], secondaryGridOrigin, secondaryCellSize).Position;

		return true;
	}

	private bool HandleHideoutStorageRelease(Vector2 click)
	{
		if (_hasDraggedHideoutItem)
		{
			GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize);
			GetSecondaryStorageGridLayout(out Rect2 secondaryGridRect, out Vector2 secondaryGridOrigin, out Vector2 secondaryCellSize);
			bool clickedStash = stashGridRect.HasPoint(click);
			bool clickedSecondary = secondaryGridRect.HasPoint(click);

			if (clickedStash || clickedSecondary)
			{
				Vector2 cellSize = clickedStash ? stashCellSize : secondaryCellSize;
				Vector2I cell = GetDraggedHideoutTargetCell(clickedStash ? stashGridOrigin : secondaryGridOrigin, cellSize);
				if (clickedStash)
				{
					if (TryPlaceDraggedHideoutItemInStash(cell))
					{
						return true;
					}
				}
				else if (TryPlaceDraggedHideoutItemInSecondary(cell))
				{
					return true;
				}
			}

			RestoreDraggedHideoutItem();
			return true;
		}

		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		return false;
	}

	private void TryBeginPendingHideoutDrag()
	{
		if (!_pendingHideoutDrag || _pendingHideoutDragIndex < 0 || _hasDraggedHideoutItem)
		{
			return;
		}

		if (_pendingHideoutDragFromLoadout)
		{
			if (_pendingHideoutDragIndex >= _hideoutLoadout.Count)
			{
				_pendingHideoutDrag = false;
				_pendingHideoutDragIndex = -1;
				return;
			}

			_draggedHideoutItem = _hideoutLoadout[_pendingHideoutDragIndex];
			_draggedHideoutOriginalCell = _draggedHideoutItem.Cell;
			_draggedHideoutGrabOffset = _pendingHideoutGrabOffset;
			_draggedHideoutFromLoadout = true;
			_hasDraggedHideoutItem = true;
			_hideoutLoadout.RemoveAt(_pendingHideoutDragIndex);
			_selectedLoadoutIndex = -1;
			_selectedSettlementIndex = -1;
		}
		else
		{
			if (_pendingHideoutDragIndex >= _stash.Count)
			{
				_pendingHideoutDrag = false;
				_pendingHideoutDragIndex = -1;
				return;
			}

			_draggedHideoutItem = _stash[_pendingHideoutDragIndex];
			_draggedHideoutOriginalCell = _draggedHideoutItem.Cell;
			_draggedHideoutGrabOffset = _pendingHideoutGrabOffset;
			_draggedHideoutFromLoadout = false;
			_hasDraggedHideoutItem = true;
			_stash.RemoveAt(_pendingHideoutDragIndex);
			_selectedStashIndex = -1;
		}

		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		_pendingHideoutGrabOffset = Vector2.Zero;
	}

	private void RestoreDraggedHideoutItem()
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return;
		}

		_draggedHideoutItem.Cell = _draggedHideoutOriginalCell;
		if (_draggedHideoutFromLoadout)
		{
			_hideoutLoadout.Add(_draggedHideoutItem);
		}
		else
		{
			_stash.Add(_draggedHideoutItem);
		}

		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		_pendingHideoutGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
	}

	private Rect2 GetStorageItemRect(BackpackItem item, Vector2 gridOrigin, Vector2 cellSize)
	{
		return new Rect2(
			gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
			new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
	}

	private Vector2I GetDraggedHideoutTargetCell(Vector2 gridOrigin, Vector2 cellSize)
	{
		Vector2 dragOrigin = GetViewport().GetMousePosition() - _draggedHideoutGrabOffset;
		Vector2 local = dragOrigin - gridOrigin + new Vector2(Ui(1f), Ui(1f));
		return new Vector2I(
			Mathf.FloorToInt(local.X / cellSize.X),
			Mathf.FloorToInt(local.Y / cellSize.Y));
	}

	private bool TryPlaceDraggedHideoutItemInStash(Vector2I cell)
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return false;
		}

		if (cell.X < 0 || cell.Y < 0
			|| cell.X + _draggedHideoutItem.Size.X > StashGridWidth
			|| cell.Y + _draggedHideoutItem.Size.Y > StashGridHeight
			|| !IsStorageAreaFree(cell, _draggedHideoutItem.Size, _stash))
		{
			return false;
		}

		_draggedHideoutItem.Cell = cell;
		_stash.Add(_draggedHideoutItem);
		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private bool TryPlaceDraggedHideoutItemInSecondary(Vector2I cell)
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return false;
		}

		List<BackpackCapacityBlock> blocks = BuildHideoutLoadoutCapacityBlocks();
		if (!IsAreaEnabledInBlocks(cell, _draggedHideoutItem.Size, blocks) || !IsStorageAreaFree(cell, _draggedHideoutItem.Size, _hideoutLoadout))
		{
			return false;
		}

		_draggedHideoutItem.Cell = cell;
		_hideoutLoadout.Add(_draggedHideoutItem);
		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private void GetHideoutLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 shopRect, out Rect2 loadoutRect)
	{
		Vector2 viewport = GetViewportRect().Size;
		Vector2 panelSize = new(
			Mathf.Clamp(viewport.X - Ui(120f), 1100f, 1480f),
			Mathf.Clamp(viewport.Y - Ui(90f), 660f, 900f));
		panel = new Rect2((viewport - panelSize) * 0.5f, panelSize);

		float contentTop = panel.Position.Y + Ui(272f);
		float contentBottom = panel.End.Y - Ui(18f);
		float contentHeight = Mathf.Max(Ui(260f), contentBottom - contentTop);
		float gap = Ui(20f);
		float stashWidth = Mathf.Max(Ui(320f), panel.Size.X * 0.46f);
		float rightWidth = panel.Size.X - Ui(36f) - stashWidth - gap;
		float rightX = panel.Position.X + Ui(18f) + stashWidth + gap;
		float stackGap = Ui(16f);
		float topHeight = Mathf.Max(Ui(220f), contentHeight * 0.5f - stackGap * 0.5f);
		float bottomHeight = contentHeight - topHeight - stackGap;

		stashRect = new Rect2(new Vector2(panel.Position.X + Ui(18f), contentTop), new Vector2(stashWidth, contentHeight));
		shopRect = new Rect2(new Vector2(rightX, contentTop), new Vector2(rightWidth, topHeight));
		loadoutRect = new Rect2(new Vector2(rightX, shopRect.End.Y + stackGap), new Vector2(rightWidth, bottomHeight));
	}

	private void GetSettlementLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 backpackRect)
	{
		Vector2 viewport = GetViewportRect().Size;
		Vector2 panelSize = new(
			Mathf.Clamp(viewport.X - Ui(120f), 1080f, 1440f),
			Mathf.Clamp(viewport.Y - Ui(100f), 640f, 860f));
		panel = new Rect2((viewport - panelSize) * 0.5f, panelSize);

		float contentTop = panel.Position.Y + Ui(86f);
		float contentBottom = panel.End.Y - Ui(22f);
		float contentHeight = contentBottom - contentTop;
		float gap = Ui(18f);
		float leftWidth = Mathf.Max(Ui(320f), panel.Size.X * 0.46f);
		float rightWidth = panel.Size.X - Ui(36f) - leftWidth - gap;
		stashRect = new Rect2(new Vector2(panel.Position.X + Ui(18f), contentTop), new Vector2(leftWidth, contentHeight));
		backpackRect = new Rect2(new Vector2(stashRect.End.X + gap, contentTop), new Vector2(rightWidth, contentHeight));
	}

	private void GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize)
	{
		if (_runEnded && _showSettlementTransfer)
		{
			GetSettlementLayout(out _, out Rect2 stashRect, out _);
			stashCellSize = new Vector2(Ui(22f), Ui(22f));
			stashGridOrigin = stashRect.Position + new Vector2(Ui(14f), Ui(46f));
			stashGridRect = new Rect2(stashGridOrigin, new Vector2(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y));
			return;
		}

		GetHideoutLayout(out _, out Rect2 hideoutStashRect, out _, out _);
		stashCellSize = new Vector2(Ui(22f), Ui(22f));
		stashGridOrigin = hideoutStashRect.Position + new Vector2(Ui(14f), Ui(46f));
		stashGridRect = new Rect2(stashGridOrigin, new Vector2(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y));
	}

	private void GetSecondaryStorageGridLayout(out Rect2 gridRect, out Vector2 gridOrigin, out Vector2 cellSize)
	{
		cellSize = new Vector2(Ui(18f), Ui(18f));
		if (_runEnded && _showSettlementTransfer)
		{
			GetSettlementLayout(out _, out _, out Rect2 backpackRect);
			gridOrigin = backpackRect.Position + new Vector2(Ui(14f), Ui(42f));
		}
		else
		{
			GetHideoutLayout(out _, out _, out _, out Rect2 loadoutRect);
			gridOrigin = loadoutRect.Position + new Vector2(Ui(14f), Ui(42f));
		}

		gridRect = new Rect2(gridOrigin, new Vector2(TeamBackpackMaxWidth * cellSize.X, TeamBackpackMaxRows * cellSize.Y));
	}
	private int GetStorageItemIndexAtPoint(Vector2 point, Vector2 gridOrigin, Vector2 cellSize, List<BackpackItem> items)
	{
		for (int i = items.Count - 1; i >= 0; i--)
		{
			BackpackItem item = items[i];
			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
			if (itemRect.HasPoint(point))
			{
				return i;
			}
		}

		return -1;
	}

	private void GetBackpackPreviewLayout(out Vector2 gridOrigin, out Vector2 cellSize, out int gridWidth, out int gridHeight)
	{
		float x = _sideRect.Position.X + Ui(22f);
		float y = _sideRect.Position.Y + Ui(34f);
		y += Ui(34f);
		y += Ui(28f);
		y += Ui(26f);
		y += Ui(24f);
		y += Ui(38f);
		y += Ui(64f);
		y += Ui(28f);
		y += Ui(22f);
		y += Ui(22f);
		y += Ui(22f);
		y += Ui(20f);
		if (IsOverloaded())
		{
			y += Ui(20f);
		}

		y += _nodes[_playerNodeId].Type == NodeType.Extract ? Ui(88f) : Ui(50f);
		Vector2 origin = new(x, y + Ui(8f));
		cellSize = new Vector2(Ui(18f), Ui(18f));
		gridOrigin = origin + new Vector2(0f, Ui(30f));
		gridWidth = GetBackpackPreviewGridWidth();
		gridHeight = GetBackpackPreviewGridHeight();
	}

	private float DrawTeamBackpackPreview(Vector2 origin)
	{
		Vector2 cellSize = new(Ui(18f), Ui(18f));
		DrawString(ThemeDB.FallbackFont, origin, "队伍背包", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.9f, 0.92f, 0.96f));
		Rect2 autoPackRect = new(origin + new Vector2(Ui(114f), Ui(-6f)), new Vector2(Ui(92f), Ui(28f)));
		DrawButton(autoPackRect, "自动整理", new Color(0.28f, 0.42f, 0.26f));
		_buttons.Add(new ButtonDef(autoPackRect, "auto_pack"));
		if (_hasDraggedBackpackItem && !_inHideout && !_runEnded)
		{
			Rect2 dropDraggedRect = new(origin + new Vector2(Ui(214f), Ui(-6f)), new Vector2(Ui(92f), Ui(28f)));
			DrawButton(dropDraggedRect, "丢到地面", new Color(0.46f, 0.28f, 0.22f));
			_buttons.Add(new ButtonDef(dropDraggedRect, "drop_dragged_backpack"));
		}
		Vector2 gridOrigin = origin + new Vector2(0f, Ui(30f));
		List<BackpackCapacityBlock> blocks = BuildCurrentBackpackCapacityBlocks();
		int gridWidth = GetBackpackPreviewGridWidth();
		int gridHeight = GetBackpackPreviewGridHeight();
		for (int i = 0; i < blocks.Count; i++)
		{
			BackpackCapacityBlock block = blocks[i];
			Color blockTint = i % 2 == 0 ? new Color(0.16f, 0.19f, 0.24f, 0.55f) : new Color(0.19f, 0.17f, 0.23f, 0.55f);
			Rect2 blockRect = new(
				gridOrigin + new Vector2(block.Cell.X * cellSize.X, block.Cell.Y * cellSize.Y),
				new Vector2(block.Size.X * cellSize.X - Ui(2f), block.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(blockRect, blockTint, true);
			for (int y = 0; y < block.Size.Y; y++)
			{
				for (int x = 0; x < block.Size.X; x++)
				{
					Rect2 cellRect = new(
						gridOrigin + new Vector2((block.Cell.X + x) * cellSize.X, (block.Cell.Y + y) * cellSize.Y),
						cellSize - new Vector2(Ui(2f), Ui(2f)));
					DrawRect(cellRect, new Color(0.08f, 0.09f, 0.11f), true);
					DrawRect(cellRect, new Color(0.2f, 0.23f, 0.28f), false, 1f);
				}
			}
			DrawDashedRect(blockRect, new Color(0.82f, 0.86f, 0.94f, 0.45f));
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		string hoveredItemLabel = "";
		foreach (BackpackItem item in _runBackpack)
		{
			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, new Color(1f, 1f, 1f, 0.82f), false, 1f);
			if (itemRect.Size.X >= Ui(40f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}

			if (itemRect.HasPoint(mouse))
			{
				hoveredItemLabel = item.Label;
			}
		}

		float overflowWidth = Ui(132f);
		Vector2 overflowOrigin = gridOrigin + new Vector2(gridWidth * cellSize.X + Ui(18f), 0f);
		int overflowVisible = Mathf.Max(1, _overflowBackpackItems.Count);
		float overflowButtonHeight = _overflowBackpackItems.Count > 0 ? Ui(28f) : 0f;
		Rect2 overflowRect = new(
			overflowOrigin,
			new Vector2(overflowWidth, Mathf.Max(Ui(72f), overflowVisible * Ui(28f) + Ui(18f) + overflowButtonHeight + (_overflowBackpackItems.Count > 0 ? Ui(8f) : 0f))));
		DrawRect(overflowRect, new Color(0.07f, 0.06f, 0.05f, 0.92f), true);
		DrawRect(overflowRect, _overflowBackpackItems.Count > 0 ? new Color(0.8f, 0.44f, 0.4f, 0.95f) : new Color(0.3f, 0.32f, 0.36f, 0.85f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, overflowRect.Position + new Vector2(Ui(8f), Ui(16f)), "待整理", HorizontalAlignment.Left, overflowRect.Size.X - Ui(12f), UiFont(12), Colors.White);
		if (_overflowBackpackItems.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, overflowRect.Position + new Vector2(Ui(8f), Ui(38f)), "无物品", HorizontalAlignment.Left, overflowRect.Size.X - Ui(12f), UiFont(11), new Color(0.72f, 0.76f, 0.82f));
		}
		else
		{
			for (int i = 0; i < _overflowBackpackItems.Count; i++)
			{
				BackpackItem item = _overflowBackpackItems[i];
				Rect2 overflowItemRect = new(
					overflowRect.Position + new Vector2(Ui(8f), Ui(24f) + i * Ui(28f)),
					new Vector2(overflowRect.Size.X - Ui(16f), Ui(22f)));
				Color itemColor = GetGridRarityColor(item.Rarity);
				DrawRect(overflowItemRect, new Color(itemColor.R, itemColor.G, itemColor.B, 0.92f), true);
				DrawRect(overflowItemRect, new Color(1f, 1f, 1f, 0.75f), false, 1f);
				DrawString(ThemeDB.FallbackFont, overflowItemRect.Position + new Vector2(Ui(4f), Ui(14f)), $"{item.Label} {item.Size.X}x{item.Size.Y}", HorizontalAlignment.Left, overflowItemRect.Size.X - Ui(8f), UiFont(10), Colors.Black);
				if (overflowItemRect.HasPoint(mouse))
				{
					hoveredItemLabel = item.Label;
				}
			}

			Rect2 dropRect = new(
				new Vector2(overflowRect.Position.X + Ui(8f), overflowRect.End.Y - Ui(32f)),
				new Vector2(overflowRect.Size.X - Ui(16f), Ui(24f)));
			DrawButton(dropRect, "丢到地面", new Color(0.46f, 0.28f, 0.22f));
			_buttons.Add(new ButtonDef(dropRect, "drop_overflow"));
		}

		if (!string.IsNullOrEmpty(hoveredItemLabel))
		{
			DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f)), hoveredItemLabel);
		}

		return Ui(36f) + Mathf.Max(gridHeight * cellSize.Y, overflowRect.Size.Y);
	}

	private void DrawDraggedBackpackOverlay()
	{
		if (!_hasDraggedBackpackItem || _draggedBackpackItem == null || _inHideout || _runEnded)
		{
			return;
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		Vector2 cellSize = new(Ui(18f), Ui(18f));
		Vector2 dragSize = new(_draggedBackpackItem.Size.X * cellSize.X - Ui(2f), _draggedBackpackItem.Size.Y * cellSize.Y - Ui(2f));
		Vector2 drawPos = mouse - _draggedBackpackGrabOffset;
		Rect2 dragRect = new(drawPos, dragSize);
		Color dragColor = GetGridRarityColor(_draggedBackpackItem.Rarity);

		GetBackpackPreviewLayout(out Vector2 gridOrigin, out Vector2 previewCellSize, out int _, out int _);
		Vector2I previewCell = GetDraggedBackpackTargetCell(gridOrigin, previewCellSize);
		if (previewCell.X >= 0 && previewCell.Y >= 0
			&& previewCell.X + _draggedBackpackItem.Size.X <= TeamBackpackMaxWidth
			&& previewCell.Y + _draggedBackpackItem.Size.Y <= TeamBackpackMaxRows)
		{
			Rect2 targetRect = GetBackpackItemRect(new BackpackItem { Cell = previewCell, Size = _draggedBackpackItem.Size }, gridOrigin, previewCellSize);
			bool canPlace = IsBackpackAreaEnabled(previewCell, _draggedBackpackItem.Size) && IsBackpackAreaFree(previewCell, _draggedBackpackItem.Size);
			DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
			DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
		}

		DrawRect(dragRect, new Color(dragColor.R, dragColor.G, dragColor.B, 0.84f), true);
		DrawRect(dragRect, new Color(1f, 1f, 1f, 0.9f), false, 1f);
		DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f) + dragRect.Size.Y + Ui(4f)), _draggedBackpackItem.Label);
	}

	private void DrawDraggedHideoutOverlay()
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return;
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		Vector2 cellSize = _draggedHideoutFromLoadout ? new Vector2(Ui(18f), Ui(18f)) : new Vector2(Ui(22f), Ui(22f));
		Vector2 dragSize = new(_draggedHideoutItem.Size.X * cellSize.X - Ui(2f), _draggedHideoutItem.Size.Y * cellSize.Y - Ui(2f));
		Vector2 drawPos = mouse - _draggedHideoutGrabOffset;
		Rect2 dragRect = new(drawPos, dragSize);
		Color dragColor = GetGridRarityColor(_draggedHideoutItem.Rarity);

		GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize);
		GetSecondaryStorageGridLayout(out Rect2 secondaryGridRect, out Vector2 secondaryGridOrigin, out Vector2 secondaryCellSize);
		if (stashGridRect.HasPoint(mouse))
		{
			Vector2I cell = GetDraggedHideoutTargetCell(stashGridOrigin, stashCellSize);
			if (cell.X >= 0 && cell.Y >= 0 && cell.X + _draggedHideoutItem.Size.X <= StashGridWidth && cell.Y + _draggedHideoutItem.Size.Y <= StashGridHeight)
			{
				Rect2 targetRect = GetStorageItemRect(new BackpackItem { Cell = cell, Size = _draggedHideoutItem.Size }, stashGridOrigin, stashCellSize);
				bool canPlace = IsStorageAreaFree(cell, _draggedHideoutItem.Size, _stash);
				DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
				DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
			}
		}
		else if (secondaryGridRect.HasPoint(mouse))
		{
			Vector2I cell = GetDraggedHideoutTargetCell(secondaryGridOrigin, secondaryCellSize);
			Rect2 targetRect = GetStorageItemRect(new BackpackItem { Cell = cell, Size = _draggedHideoutItem.Size }, secondaryGridOrigin, secondaryCellSize);
			bool canPlace = IsAreaEnabledInBlocks(cell, _draggedHideoutItem.Size, BuildHideoutLoadoutCapacityBlocks()) && IsStorageAreaFree(cell, _draggedHideoutItem.Size, _hideoutLoadout);
			DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
			DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
		}

		DrawRect(dragRect, new Color(dragColor.R, dragColor.G, dragColor.B, 0.84f), true);
		DrawRect(dragRect, new Color(1f, 1f, 1f, 0.9f), false, 1f);
		DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f) + dragRect.Size.Y + Ui(4f)), _draggedHideoutItem.Label);
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

	private int CountTakeableContainerItems(LootContainer container)
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
				if (item.Revealed && !item.Taken)
				{
					count++;
				}
			}

			return count;
		}

		return count + container.VisibleItems.Count;
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
		GetContainerPopupGridLayout(container, out Rect2 panel, out Vector2 gridOrigin, out Vector2 cellSize);
		DrawRect(panel, new Color(0.08f, 0.09f, 0.11f, 0.7f), true);
		DrawRect(panel, new Color(1f, 1f, 1f, 0.82f), false, 2f);
		Rect2 titleBar = new(panel.Position + new Vector2(1f, 1f), new Vector2(panel.Size.X - 2f, 30f));
		DrawRect(titleBar, new Color(0.12f, 0.13f, 0.16f, 0.72f), true);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14f, 21f), container.Label, HorizontalAlignment.Left, 190f, 14, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(panel.Size.X - 102f, 21f), GetContainerKindLabel(container.Kind), HorizontalAlignment.Left, 68f, 12, new Color(0.93f, 0.84f, 0.58f));
		int takeableCount = CountTakeableContainerItems(container);
		if (takeableCount > 0)
		{
			Rect2 takeAllRect = new(new Vector2(panel.End.X - 92f, panel.Position.Y + 4f), new Vector2(78f, 22f));
			DrawButton(takeAllRect, "拾取全部", new Color(0.28f, 0.46f, 0.62f));
			_buttons.Add(new ButtonDef(takeAllRect, "take_all", _selectedContainerIndex));
		}
		if (_hasDraggedBackpackItem && _draggedBackpackItem != null)
		{
			DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14f, 38f), "背包物品已拿起：点击下方容器网格即可放回。", HorizontalAlignment.Left, panel.Size.X - 28f, 11, new Color(0.92f, 0.94f, 0.78f));
		}
		float rowY = panel.Position.Y + (_hasDraggedBackpackItem && _draggedBackpackItem != null ? 56f : 42f);
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
		for (int y = 0; y < container.GridSize.Y; y++)
		{
			for (int x = 0; x < container.GridSize.X; x++)
			{
				Rect2 cellRect = new(gridOrigin + new Vector2(x * cellSize.X, y * cellSize.Y), cellSize - new Vector2(2f, 2f));
				DrawRect(cellRect, new Color(0.09f, 0.1f, 0.12f), true);
				DrawRect(cellRect, new Color(0.24f, 0.26f, 0.3f), false, 1f);
			}
		}

		if (_hasDraggedBackpackItem && _draggedBackpackItem != null)
		{
			Vector2I targetCell = GetDraggedBackpackTargetCell(gridOrigin, cellSize);
			if (targetCell.X >= 0 && targetCell.Y >= 0
				&& targetCell.X + _draggedBackpackItem.Size.X <= container.GridSize.X
				&& targetCell.Y + _draggedBackpackItem.Size.Y <= container.GridSize.Y)
			{
				Rect2 targetRect = new(
					gridOrigin + new Vector2(targetCell.X * cellSize.X, targetCell.Y * cellSize.Y),
					new Vector2(_draggedBackpackItem.Size.X * cellSize.X - 2f, _draggedBackpackItem.Size.Y * cellSize.Y - 2f));
				bool canPlace = IsGridAreaFree(container, targetCell, _draggedBackpackItem.Size);
				DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
				DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
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

	private void GetContainerPopupGridLayout(LootContainer container, out Rect2 panel, out Vector2 gridOrigin, out Vector2 cellSize)
	{
		EnsureContainerGrid(container);
		cellSize = new Vector2(26f, 26f);
		float equipmentHeight = container.Kind == ContainerKind.EliteCorpse ? 118f : 0f;
		float gridWidth = container.GridSize.X * cellSize.X + 26f;
		Vector2 panelSize = new(Mathf.Max(gridWidth + 28f, 356f), container.GridSize.Y * cellSize.Y + 96f + equipmentHeight);
		panel = new Rect2((GetViewportRect().Size - panelSize) / 2f, panelSize);
		float rowY = panel.Position.Y + (_hasDraggedBackpackItem && _draggedBackpackItem != null ? 56f : 42f);
		if (container.Kind == ContainerKind.EliteCorpse)
		{
			rowY += 16f;
			rowY += container.EquippedItems.Count * 34f;
			rowY += 8f;
		}

		rowY += 16f;
		gridOrigin = new Vector2(panel.Position.X + (panel.Size.X - gridWidth) * 0.5f, rowY);
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

		if (_runFailed)
		{
			Rect2 rect = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(140f, 30f));
			DrawButton(rect, "重新开始", new Color(0.26f, 0.45f, 0.62f));
			_buttons.Add(new ButtonDef(rect, "restart"));
			return;
		}

		Rect2 settlementRect = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(164f, 30f));
		DrawButton(settlementRect, "进入结算转移", new Color(0.26f, 0.45f, 0.62f));
		_buttons.Add(new ButtonDef(settlementRect, "open_settlement"));
		Rect2 restartRect = new(new Vector2(settlementRect.End.X + 12f, panel.End.Y - 54f), new Vector2(140f, 30f));
		DrawButton(restartRect, "直接返回", new Color(0.32f, 0.34f, 0.4f));
		_buttons.Add(new ButtonDef(restartRect, "restart"));
	}

	private void DrawSettlementTransfer()
	{
		GetSettlementLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 backpackRect);
		DrawRect(panel, new Color(0.04f, 0.04f, 0.05f, 0.97f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(Ui(24f), Ui(34f)), "撤离结算", HorizontalAlignment.Left, -1f, UiFont(24), Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(Ui(24f), Ui(58f)), "左侧是仓库，右侧是本次带回的局外背包。双向拖动后，点完成结算。", HorizontalAlignment.Left, panel.Size.X - Ui(240f), UiFont(12), new Color(0.82f, 0.88f, 0.94f));
		Rect2 finishRect = new(new Vector2(panel.End.X - Ui(156f), panel.Position.Y + Ui(18f)), new Vector2(Ui(132f), Ui(32f)));
		DrawButton(finishRect, "完成结算", new Color(0.28f, 0.48f, 0.34f));
		_buttons.Add(new ButtonDef(finishRect, "finish_settlement"));
		Rect2 autoPackRect = new(new Vector2(panel.End.X - Ui(264f), panel.Position.Y + Ui(18f)), new Vector2(Ui(92f), Ui(32f)));
		DrawButton(autoPackRect, "整理背包", new Color(0.28f, 0.42f, 0.26f));
		_buttons.Add(new ButtonDef(autoPackRect, "auto_pack_settlement"));
		Rect2 transferAllRect = new(new Vector2(panel.End.X - Ui(382f), panel.Position.Y + Ui(18f)), new Vector2(Ui(108f), Ui(32f)));
		DrawButton(transferAllRect, "全部入仓", new Color(0.26f, 0.42f, 0.58f));
		_buttons.Add(new ButtonDef(transferAllRect, "settlement_all_to_stash"));

		DrawRect(stashRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(backpackRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(stashRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawRect(backpackRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, stashRect.Position + new Vector2(Ui(14f), Ui(24f)), "仓库", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);
		DrawString(ThemeDB.FallbackFont, backpackRect.Position + new Vector2(Ui(14f), Ui(24f)), "局外背包", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);

		Vector2 stashCellSize = new(Ui(22f), Ui(22f));
		Vector2 stashGridOrigin = stashRect.Position + new Vector2(Ui(14f), Ui(46f));
		Rect2 stashGridRect = new(stashGridOrigin, new Vector2(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y));
		DrawRect(stashGridRect, new Color(0.07f, 0.08f, 0.1f), true);
		for (int yCell = 0; yCell < StashGridHeight; yCell++)
		{
			for (int xCell = 0; xCell < StashGridWidth; xCell++)
			{
				Rect2 cellRect = new(
					stashGridOrigin + new Vector2(xCell * stashCellSize.X, yCell * stashCellSize.Y),
					stashCellSize - new Vector2(Ui(2f), Ui(2f)));
				DrawRect(cellRect, new Color(0.11f, 0.12f, 0.15f), true);
				DrawRect(cellRect, new Color(0.24f, 0.27f, 0.32f), false, 1f);
			}
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		string hoverLabel = "";
		for (int i = 0; i < _stash.Count; i++)
		{
			BackpackItem item = _stash[i];
			Rect2 itemRect = new(
				stashGridOrigin + new Vector2(item.Cell.X * stashCellSize.X, item.Cell.Y * stashCellSize.Y),
				new Vector2(item.Size.X * stashCellSize.X - Ui(2f), item.Size.Y * stashCellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, i == _selectedStashIndex ? new Color(1f, 0.94f, 0.62f, 0.96f) : new Color(1f, 1f, 1f, 0.8f), false, i == _selectedStashIndex ? 2f : 1f);
			if (itemRect.Size.X >= Ui(42f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}

			if (itemRect.HasPoint(mouse))
			{
				hoverLabel = item.Label;
			}

			_buttons.Add(new ButtonDef(itemRect, "select_stash", i));
		}

		Vector2 cellSize = new(Ui(18f), Ui(18f));
		Vector2 gridOrigin = backpackRect.Position + new Vector2(Ui(14f), Ui(42f));
		List<BackpackCapacityBlock> blocks = BuildHideoutLoadoutCapacityBlocks();
		for (int i = 0; i < blocks.Count; i++)
		{
			BackpackCapacityBlock block = blocks[i];
			Rect2 blockRect = new(
				gridOrigin + new Vector2(block.Cell.X * cellSize.X, block.Cell.Y * cellSize.Y),
				new Vector2(block.Size.X * cellSize.X - Ui(2f), block.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(blockRect, i == 0 ? new Color(0.18f, 0.2f, 0.26f, 0.65f) : new Color(0.16f, 0.18f, 0.22f, 0.6f), true);
			DrawDashedRect(blockRect, new Color(0.82f, 0.86f, 0.94f, 0.4f));
			for (int by = 0; by < block.Size.Y; by++)
			{
				for (int bx = 0; bx < block.Size.X; bx++)
				{
					Rect2 cellRect = new(
						gridOrigin + new Vector2((block.Cell.X + bx) * cellSize.X, (block.Cell.Y + by) * cellSize.Y),
						cellSize - new Vector2(Ui(2f), Ui(2f)));
					DrawRect(cellRect, new Color(0.08f, 0.09f, 0.11f), true);
					DrawRect(cellRect, new Color(0.2f, 0.23f, 0.28f), false, 1f);
				}
			}
		}

		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			BackpackItem item = _hideoutLoadout[i];
			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, i == _selectedSettlementIndex ? new Color(1f, 0.94f, 0.62f, 0.96f) : new Color(1f, 1f, 1f, 0.8f), false, i == _selectedSettlementIndex ? 2f : 1f);
			if (itemRect.Size.X >= Ui(42f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}

			if (itemRect.HasPoint(mouse))
			{
				hoverLabel = item.Label;
			}

			_buttons.Add(new ButtonDef(itemRect, "select_settlement", i));
		}

		DrawString(ThemeDB.FallbackFont, new Vector2(backpackRect.Position.X + Ui(14f), backpackRect.End.Y - Ui(22f)),
			$"当前保留 { _hideoutLoadout.Count } 件物品，未转入仓库的内容会继续留在局外背包。",
			HorizontalAlignment.Left, backpackRect.Size.X - Ui(28f), UiFont(12), new Color(0.82f, 0.88f, 0.94f));

		if (_hasDraggedHideoutItem && _draggedHideoutItem != null)
		{
			hoverLabel = _draggedHideoutItem.Label;
		}

		if (!string.IsNullOrEmpty(hoverLabel))
		{
			DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f)), hoverLabel);
		}
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
