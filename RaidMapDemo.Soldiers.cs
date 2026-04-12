using Godot;

public partial class RaidMapDemo
{
	private const int SoldierPageSize = 4;

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
		SoldierClass.ShieldPlusOne => "钢盔盾卫",
		SoldierClass.ShieldPlusTwo => "壁垒盾卫",
		SoldierClass.Pike => "枪兵",
		SoldierClass.ElitePike => "精锐枪兵",
		SoldierClass.IronhelmPike => "钢盔枪卫",
		SoldierClass.VanguardPike => "御锋枪卫",
		SoldierClass.Blade => "刀兵",
		SoldierClass.EliteBlade => "精锐刀兵",
		SoldierClass.IronhelmBlade => "钢盔刀卫",
		SoldierClass.VanguardBlade => "断阵刀卫",
		SoldierClass.Archer => "弓兵",
		SoldierClass.EliteArcher => "精锐弓兵",
		SoldierClass.IronhelmArcher => "钢盔猎手",
		SoldierClass.VanguardArcher => "穿杨射手",
		SoldierClass.Cavalry => "骑兵",
		_ => "未知",
	};

	private int GetSoldierPromotionRequirement(SoldierClass targetClass) => targetClass switch
	{
		SoldierClass.Shield => 2,
		SoldierClass.EliteShield => 6,
		SoldierClass.ShieldPlusOne => 10,
		SoldierClass.ShieldPlusTwo => 15,
		SoldierClass.Pike => 2,
		SoldierClass.ElitePike => 6,
		SoldierClass.IronhelmPike => 10,
		SoldierClass.VanguardPike => 15,
		SoldierClass.Blade => 2,
		SoldierClass.EliteBlade => 6,
		SoldierClass.IronhelmBlade => 10,
		SoldierClass.VanguardBlade => 15,
		SoldierClass.Archer => 2,
		SoldierClass.EliteArcher => 6,
		SoldierClass.IronhelmArcher => 10,
		SoldierClass.VanguardArcher => 15,
		SoldierClass.Cavalry => 3,
		_ => 0,
	};

	private int GetSoldierPromotionCost(SoldierClass targetClass) => targetClass switch
	{
		SoldierClass.Shield => 18,
		SoldierClass.EliteShield => 42,
		SoldierClass.ShieldPlusOne => 70,
		SoldierClass.ShieldPlusTwo => 110,
		SoldierClass.Pike => 18,
		SoldierClass.ElitePike => 42,
		SoldierClass.IronhelmPike => 70,
		SoldierClass.VanguardPike => 110,
		SoldierClass.Blade => 18,
		SoldierClass.EliteBlade => 42,
		SoldierClass.IronhelmBlade => 70,
		SoldierClass.VanguardBlade => 110,
		SoldierClass.Archer => 22,
		SoldierClass.EliteArcher => 42,
		SoldierClass.IronhelmArcher => 70,
		SoldierClass.VanguardArcher => 110,
		SoldierClass.Cavalry => 40,
		_ => 0,
	};

	private bool CanPromoteSoldier(SoldierRecord soldier, SoldierClass targetClass)
	{
		bool validTarget = soldier.Class switch
		{
			SoldierClass.Recruit => targetClass is SoldierClass.Shield or SoldierClass.Pike or SoldierClass.Blade or SoldierClass.Archer,
			SoldierClass.Shield => targetClass == SoldierClass.EliteShield,
			SoldierClass.EliteShield => targetClass == SoldierClass.ShieldPlusOne,
			SoldierClass.ShieldPlusOne => targetClass == SoldierClass.ShieldPlusTwo,
			SoldierClass.Pike => targetClass == SoldierClass.ElitePike,
			SoldierClass.ElitePike => targetClass == SoldierClass.IronhelmPike,
			SoldierClass.IronhelmPike => targetClass == SoldierClass.VanguardPike,
			SoldierClass.Blade => targetClass == SoldierClass.EliteBlade,
			SoldierClass.EliteBlade => targetClass == SoldierClass.IronhelmBlade,
			SoldierClass.IronhelmBlade => targetClass == SoldierClass.VanguardBlade,
			SoldierClass.Archer => targetClass == SoldierClass.EliteArcher,
			SoldierClass.EliteArcher => targetClass == SoldierClass.IronhelmArcher,
			SoldierClass.IronhelmArcher => targetClass == SoldierClass.VanguardArcher,
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
		SoldierClass.ShieldPlusOne => new Color(0.74f, 1f, 0.86f),
		SoldierClass.ShieldPlusTwo => new Color(0.9f, 1f, 0.9f),
		SoldierClass.Pike => new Color(0.88f, 0.8f, 0.48f),
		SoldierClass.ElitePike => new Color(0.94f, 0.84f, 0.52f),
		SoldierClass.IronhelmPike => new Color(1f, 0.88f, 0.58f),
		SoldierClass.VanguardPike => new Color(1f, 0.94f, 0.7f),
		SoldierClass.Blade => new Color(0.94f, 0.52f, 0.44f),
		SoldierClass.EliteBlade => new Color(1f, 0.6f, 0.5f),
		SoldierClass.IronhelmBlade => new Color(1f, 0.68f, 0.56f),
		SoldierClass.VanguardBlade => new Color(1f, 0.76f, 0.62f),
		SoldierClass.Archer => new Color(0.6f, 0.78f, 0.98f),
		SoldierClass.EliteArcher => new Color(0.66f, 0.84f, 1f),
		SoldierClass.IronhelmArcher => new Color(0.72f, 0.9f, 1f),
		SoldierClass.VanguardArcher => new Color(0.82f, 0.95f, 1f),
		SoldierClass.Cavalry => new Color(0.92f, 0.7f, 0.34f),
		_ => Colors.White,
	};

	private bool IsSoldierRangedClass(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.Archer or SoldierClass.EliteArcher or SoldierClass.IronhelmArcher or SoldierClass.VanguardArcher;
	}

	private bool IsShieldLineClass(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.Shield or SoldierClass.EliteShield or SoldierClass.ShieldPlusOne or SoldierClass.ShieldPlusTwo;
	}

	private bool IsPikeLineClass(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.Pike or SoldierClass.ElitePike or SoldierClass.IronhelmPike or SoldierClass.VanguardPike;
	}

	private bool IsBladeLineClass(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.Blade or SoldierClass.EliteBlade or SoldierClass.IronhelmBlade or SoldierClass.VanguardBlade;
	}

	private bool HasStrengthenedPikePassive(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.IronhelmPike or SoldierClass.VanguardPike;
	}

	private bool HasEnhancedPikeThrust(SoldierClass soldierClass)
	{
		return soldierClass == SoldierClass.VanguardPike;
	}

	private bool HasStrengthenedBladePassive(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.IronhelmBlade or SoldierClass.VanguardBlade;
	}

	private bool HasEnhancedBladeRush(SoldierClass soldierClass)
	{
		return soldierClass == SoldierClass.VanguardBlade;
	}

	private bool HasStrengthenedArcherPassive(SoldierClass soldierClass)
	{
		return soldierClass is SoldierClass.IronhelmArcher or SoldierClass.VanguardArcher;
	}

	private bool HasEnhancedSplitArrow(SoldierClass soldierClass)
	{
		return soldierClass == SoldierClass.VanguardArcher;
	}

	private SoldierActiveSkill GetSoldierActiveSkill(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Archer => SoldierActiveSkill.None,
		SoldierClass.EliteShield => SoldierActiveSkill.ShieldRush,
		SoldierClass.ShieldPlusOne => SoldierActiveSkill.ShieldRush,
		SoldierClass.ShieldPlusTwo => SoldierActiveSkill.ShieldRush,
		SoldierClass.ElitePike => SoldierActiveSkill.PikeThrust,
		SoldierClass.IronhelmPike => SoldierActiveSkill.PikeThrust,
		SoldierClass.VanguardPike => SoldierActiveSkill.PikeThrust,
		SoldierClass.EliteBlade => SoldierActiveSkill.BladeRush,
		SoldierClass.IronhelmBlade => SoldierActiveSkill.BladeRush,
		SoldierClass.VanguardBlade => SoldierActiveSkill.BladeRush,
		SoldierClass.EliteArcher => SoldierActiveSkill.SplitArrow,
		SoldierClass.IronhelmArcher => SoldierActiveSkill.SplitArrow,
		SoldierClass.VanguardArcher => SoldierActiveSkill.SplitArrow,
		SoldierClass.Recruit or SoldierClass.Cavalry => SoldierActiveSkill.Sprint,
		_ => SoldierActiveSkill.None,
	};

	private SoldierPassiveSkill GetSoldierPassiveSkill(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Shield => SoldierPassiveSkill.MissileGuard,
		SoldierClass.EliteShield => SoldierPassiveSkill.MissileGuard,
		SoldierClass.ShieldPlusOne => SoldierPassiveSkill.MissileGuard,
		SoldierClass.ShieldPlusTwo => SoldierPassiveSkill.MissileGuard,
		SoldierClass.Pike => SoldierPassiveSkill.Brace,
		SoldierClass.ElitePike => SoldierPassiveSkill.Brace,
		SoldierClass.IronhelmPike => SoldierPassiveSkill.Brace,
		SoldierClass.VanguardPike => SoldierPassiveSkill.Brace,
		SoldierClass.Blade => SoldierPassiveSkill.Executioner,
		SoldierClass.EliteBlade => SoldierPassiveSkill.Executioner,
		SoldierClass.IronhelmBlade => SoldierPassiveSkill.Executioner,
		SoldierClass.VanguardBlade => SoldierPassiveSkill.Executioner,
		SoldierClass.Archer => SoldierPassiveSkill.Deadeye,
		SoldierClass.EliteArcher => SoldierPassiveSkill.Deadeye,
		SoldierClass.IronhelmArcher => SoldierPassiveSkill.Deadeye,
		SoldierClass.VanguardArcher => SoldierPassiveSkill.Deadeye,
		_ => SoldierPassiveSkill.None,
	};

	private string GetSoldierActiveSkillLabel(SoldierClass soldierClass) => GetSoldierActiveSkill(soldierClass) switch
	{
		SoldierActiveSkill.Sprint => "主动：跑动",
		SoldierActiveSkill.ShieldRush => soldierClass == SoldierClass.ShieldPlusTwo
			? "主动：盾冲·壁垒（耗体力，5秒冷却）"
			: "主动：盾冲（耗体力，5秒冷却）",
		SoldierActiveSkill.PikeThrust => HasEnhancedPikeThrust(soldierClass)
			? "主动：贯阵突刺（耗体力，5秒冷却）"
			: "主动：挺枪突刺（耗体力，5秒冷却）",
		SoldierActiveSkill.BladeRush => HasEnhancedBladeRush(soldierClass)
			? "主动：断阵突袭（耗体力，4.5秒冷却）"
			: "主动：突进斩（耗体力，4.5秒冷却）",
		SoldierActiveSkill.SplitArrow => HasEnhancedSplitArrow(soldierClass)
			? "主动：裂空箭雨（耗体力，5秒冷却）"
			: "主动：分裂箭（耗体力，5秒冷却）",
		_ => "主动：无",
	};

	private string GetSoldierPassiveSkillLabel(SoldierClass soldierClass) => GetSoldierPassiveSkill(soldierClass) switch
	{
		SoldierPassiveSkill.MissileGuard => soldierClass switch
		{
			SoldierClass.ShieldPlusOne or SoldierClass.ShieldPlusTwo => "被动：对远程攻击减伤 50%，并有概率格挡任意伤害",
			_ => "被动：对远程攻击减伤 50%",
		},
		SoldierPassiveSkill.Brace => HasStrengthenedPikePassive(soldierClass)
			? "被动：拒马列阵强化，先手命中时额外增伤并显著加强击退与硬直"
			: "被动：拒马列阵，攻击距离外缘命中时造成更强击退与硬直",
		SoldierPassiveSkill.Executioner => HasStrengthenedBladePassive(soldierClass)
			? "被动：追猎强化，对残血或硬直目标增伤，并缩短自身攻击冷却"
			: "被动：追猎，对残血或硬直目标增伤并加重硬直",
		SoldierPassiveSkill.Deadeye => HasStrengthenedArcherPassive(soldierClass)
			? "被动：鹰眼强化，暴击率更高，暴击伤害更强"
			: "被动：鹰眼，远程攻击有概率造成暴击",
		_ => "被动：无",
	};

	private int GetSoldierStrengthValue(SoldierClass soldierClass) => soldierClass switch
	{
		SoldierClass.Recruit => 1,
		SoldierClass.Shield => 2,
		SoldierClass.EliteShield => 3,
		SoldierClass.ShieldPlusOne => 4,
		SoldierClass.ShieldPlusTwo => 5,
		SoldierClass.Pike => 2,
		SoldierClass.ElitePike => 3,
		SoldierClass.IronhelmPike => 4,
		SoldierClass.VanguardPike => 5,
		SoldierClass.Blade => 2,
		SoldierClass.EliteBlade => 3,
		SoldierClass.IronhelmBlade => 4,
		SoldierClass.VanguardBlade => 5,
		SoldierClass.Archer => 2,
		SoldierClass.EliteArcher => 3,
		SoldierClass.IronhelmArcher => 4,
		SoldierClass.VanguardArcher => 5,
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
		unit.Name = $"{soldier.Name}路{GetSoldierClassLabel(soldier.Class)}";
		unit.Armor = 0;
		unit.CanSprint = !unit.IsRanged;
		unit.ActiveSkill = unit.CanSprint ? SoldierActiveSkill.Sprint : SoldierActiveSkill.None;
		unit.PassiveSkill = SoldierPassiveSkill.None;
		unit.ProjectileDamageScale = 1f;
		unit.BlockAnyDamageChance = 0f;
		unit.AttackCycleScale = 1f;

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
				unit.ActiveSkill = SoldierActiveSkill.None;
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
			case SoldierClass.ShieldPlusOne:
				unit.Hp = 24;
				unit.MaxHp = 24;
				unit.DamageMin = 3;
				unit.DamageMax = 5;
				unit.Armor = 4;
				unit.AttackRange = 30f;
				unit.Speed = 138f;
				unit.MaxStamina = 112f;
				unit.Stamina = 112f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.ShieldRush;
				unit.PassiveSkill = SoldierPassiveSkill.MissileGuard;
				unit.ProjectileDamageScale = 0.5f;
				unit.BlockAnyDamageChance = 0.18f;
				unit.AttackCycleScale = 0.86f;
				break;
			case SoldierClass.ShieldPlusTwo:
				unit.Hp = 28;
				unit.MaxHp = 28;
				unit.DamageMin = 4;
				unit.DamageMax = 7;
				unit.Armor = 5;
				unit.AttackRange = 32f;
				unit.Speed = 144f;
				unit.MaxStamina = 126f;
				unit.Stamina = 126f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.ShieldRush;
				unit.PassiveSkill = SoldierPassiveSkill.MissileGuard;
				unit.ProjectileDamageScale = 0.45f;
				unit.BlockAnyDamageChance = 0.28f;
				unit.AttackCycleScale = 0.8f;
				break;
			case SoldierClass.Pike:
				unit.Hp = 9;
				unit.MaxHp = 9;
				unit.DamageMin = 2;
				unit.DamageMax = 4;
				unit.AttackRange = 52f;
				unit.Speed = 146f;
				unit.MaxStamina = 76f;
				unit.Stamina = 76f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.None;
				unit.PassiveSkill = SoldierPassiveSkill.Brace;
				unit.AttackCycleScale = 0.98f;
				break;
			case SoldierClass.ElitePike:
				unit.Hp = 12;
				unit.MaxHp = 12;
				unit.DamageMin = 3;
				unit.DamageMax = 5;
				unit.Armor = 1;
				unit.AttackRange = 56f;
				unit.Speed = 152f;
				unit.MaxStamina = 88f;
				unit.Stamina = 88f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.PikeThrust;
				unit.PassiveSkill = SoldierPassiveSkill.Brace;
				unit.AttackCycleScale = 0.92f;
				break;
			case SoldierClass.IronhelmPike:
				unit.Hp = 15;
				unit.MaxHp = 15;
				unit.DamageMin = 4;
				unit.DamageMax = 6;
				unit.Armor = 2;
				unit.AttackRange = 58f;
				unit.Speed = 156f;
				unit.MaxStamina = 98f;
				unit.Stamina = 98f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.PikeThrust;
				unit.PassiveSkill = SoldierPassiveSkill.Brace;
				unit.AttackCycleScale = 0.88f;
				break;
			case SoldierClass.VanguardPike:
				unit.Hp = 18;
				unit.MaxHp = 18;
				unit.DamageMin = 5;
				unit.DamageMax = 8;
				unit.Armor = 3;
				unit.AttackRange = 62f;
				unit.Speed = 160f;
				unit.MaxStamina = 110f;
				unit.Stamina = 110f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.PikeThrust;
				unit.PassiveSkill = SoldierPassiveSkill.Brace;
				unit.AttackCycleScale = 0.82f;
				break;
			case SoldierClass.Blade:
				unit.Hp = 9;
				unit.MaxHp = 9;
				unit.DamageMin = 3;
				unit.DamageMax = 6;
				unit.AttackRange = 30f;
				unit.Speed = 168f;
				unit.MaxStamina = 86f;
				unit.Stamina = 86f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.None;
				unit.PassiveSkill = SoldierPassiveSkill.Executioner;
				unit.AttackCycleScale = 0.92f;
				break;
			case SoldierClass.EliteBlade:
				unit.Hp = 12;
				unit.MaxHp = 12;
				unit.DamageMin = 4;
				unit.DamageMax = 7;
				unit.Armor = 1;
				unit.AttackRange = 32f;
				unit.Speed = 174f;
				unit.MaxStamina = 96f;
				unit.Stamina = 96f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.BladeRush;
				unit.PassiveSkill = SoldierPassiveSkill.Executioner;
				unit.AttackCycleScale = 0.86f;
				break;
			case SoldierClass.IronhelmBlade:
				unit.Hp = 15;
				unit.MaxHp = 15;
				unit.DamageMin = 5;
				unit.DamageMax = 8;
				unit.Armor = 2;
				unit.AttackRange = 34f;
				unit.Speed = 178f;
				unit.MaxStamina = 108f;
				unit.Stamina = 108f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.BladeRush;
				unit.PassiveSkill = SoldierPassiveSkill.Executioner;
				unit.AttackCycleScale = 0.82f;
				break;
			case SoldierClass.VanguardBlade:
				unit.Hp = 19;
				unit.MaxHp = 19;
				unit.DamageMin = 6;
				unit.DamageMax = 10;
				unit.Armor = 3;
				unit.AttackRange = 36f;
				unit.Speed = 182f;
				unit.MaxStamina = 120f;
				unit.Stamina = 120f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.BladeRush;
				unit.PassiveSkill = SoldierPassiveSkill.Executioner;
				unit.AttackCycleScale = 0.76f;
				break;
			case SoldierClass.Archer:
				unit.Hp = 7;
				unit.MaxHp = 7;
				unit.DamageMin = 1;
				unit.DamageMax = 4;
				unit.AttackRange = 176f;
				unit.Speed = 148f;
				unit.MaxStamina = 72f;
				unit.Stamina = 72f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.None;
				unit.PassiveSkill = SoldierPassiveSkill.Deadeye;
				unit.AttackCycleScale = 0.96f;
				break;
			case SoldierClass.EliteArcher:
				unit.Hp = 10;
				unit.MaxHp = 10;
				unit.DamageMin = 2;
				unit.DamageMax = 5;
				unit.Armor = 1;
				unit.AttackRange = 184f;
				unit.Speed = 152f;
				unit.MaxStamina = 82f;
				unit.Stamina = 82f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.SplitArrow;
				unit.PassiveSkill = SoldierPassiveSkill.Deadeye;
				unit.AttackCycleScale = 0.92f;
				break;
			case SoldierClass.IronhelmArcher:
				unit.Hp = 13;
				unit.MaxHp = 13;
				unit.DamageMin = 3;
				unit.DamageMax = 6;
				unit.Armor = 2;
				unit.AttackRange = 190f;
				unit.Speed = 156f;
				unit.MaxStamina = 92f;
				unit.Stamina = 92f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.SplitArrow;
				unit.PassiveSkill = SoldierPassiveSkill.Deadeye;
				unit.AttackCycleScale = 0.88f;
				break;
			case SoldierClass.VanguardArcher:
				unit.Hp = 16;
				unit.MaxHp = 16;
				unit.DamageMin = 4;
				unit.DamageMax = 8;
				unit.Armor = 3;
				unit.AttackRange = 198f;
				unit.Speed = 160f;
				unit.MaxStamina = 104f;
				unit.Stamina = 104f;
				unit.CanSprint = false;
				unit.ActiveSkill = SoldierActiveSkill.SplitArrow;
				unit.PassiveSkill = SoldierPassiveSkill.Deadeye;
				unit.AttackCycleScale = 0.82f;
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

	private void RecruitSoldierInternal()
	{
		_soldierRoster.Add(new SoldierRecord
		{
			Name = $"士兵{_nextSoldierId}",
			Class = SoldierClass.Recruit,
			Experience = EnableSoldierBonusStartingXp ? 20 : 0,
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
		_soldierRosterPage = Mathf.Max(0, (_soldierRoster.Count - 1) / SoldierPageSize);
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
		_status = $"{soldier.Name} 已升阶为 {GetSoldierClassLabel(targetClass)}。";
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

		LogEvent($"幸存士兵因 {reason} 获得了 {amount} 点经验。");
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
}
