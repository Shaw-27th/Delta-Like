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
		ShieldPlusOne,
		ShieldPlusTwo,
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
		public float BlockAnyDamageChance;
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

	private sealed class RoomShockwaveEffect
	{
		public Vector2 Origin;
		public Vector2 Direction;
		public float Length;
		public float Radius;
		public float MaxRadius;
		public float TimeLeft;
		public float Duration;
		public bool PlayerSide;
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
	private readonly List<RoomShockwaveEffect> _roomShockwaveEffects = new();
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
		_roomShockwaveEffects.Clear();
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
				case "promote_shield_plus_one":
					PromoteSelectedSoldier(SoldierClass.ShieldPlusOne);
					return;
				case "promote_shield_plus_two":
					PromoteSelectedSoldier(SoldierClass.ShieldPlusTwo);
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
			else if (selectedSoldier.Class == SoldierClass.EliteShield)
			{
				DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY + Ui(47f)), "钢盔盾卫需求 XP 10 / 70 金。获得头盔与概率格挡。", HorizontalAlignment.Left, Ui(320f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
				Rect2 plusOneRect = new(new Vector2(soldierRect.Position.X, actionY + Ui(67f)), new Vector2(Ui(96f), Ui(24f)));
				DrawPromotionButton(plusOneRect, "钢盔", selectedSoldier, SoldierClass.ShieldPlusOne, "promote_shield_plus_one");
			}
			else if (selectedSoldier.Class == SoldierClass.ShieldPlusOne)
			{
				DrawString(ThemeDB.FallbackFont, new Vector2(soldierRect.Position.X, actionY + Ui(47f)), "壁垒盾卫需求 XP 15 / 110 金。强化盾冲并获得更华丽的大盾。", HorizontalAlignment.Left, Ui(320f), UiFont(11), new Color(0.82f, 0.88f, 0.94f));
				Rect2 plusTwoRect = new(new Vector2(soldierRect.Position.X, actionY + Ui(67f)), new Vector2(Ui(96f), Ui(24f)));
				DrawPromotionButton(plusTwoRect, "壁垒", selectedSoldier, SoldierClass.ShieldPlusTwo, "promote_shield_plus_two");
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

	private static Color GetGridRarityColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => new Color(0.82f, 0.84f, 0.87f),
		ItemRarity.Green => new Color(0.48f, 0.85f, 0.45f),
		ItemRarity.Blue => new Color(0.36f, 0.7f, 1f),
		ItemRarity.Purple => new Color(0.72f, 0.48f, 0.92f),
		_ => new Color(1f, 0.82f, 0.32f),
	};

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
