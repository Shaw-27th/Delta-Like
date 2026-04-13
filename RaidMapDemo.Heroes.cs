using Godot;

public partial class RaidMapDemo
{
	private enum HeroAttribute
	{
		Strength,
		Agility,
		Intelligence,
		Charm,
	}

	private enum HeroSkill
	{
		StrengthCore,
		AgilityCore,
		IntelligenceCore,
		CharmCore,
	}

	private void InitializeLeadHero()
	{
		_leadHero.Level = Mathf.Max(1, _leadHero.Level);
		_leadHero.Name = string.IsNullOrEmpty(_leadHero.Name) ? "领队" : _leadHero.Name;
		_leadHero.Strength = Mathf.Max(1, _leadHero.Strength);
		_leadHero.Agility = Mathf.Max(1, _leadHero.Agility);
		_leadHero.Intelligence = Mathf.Max(1, _leadHero.Intelligence);
		_leadHero.Charm = Mathf.Max(1, _leadHero.Charm);
	}

	private int GetLeadHeroExperienceToNextLevel()
	{
		return 4 + (_leadHero.Level - 1) * 3;
	}

	private int GetLeadHeroMaxHp()
	{
		return 24 + _leadHero.Strength * 8;
	}

	private int GetLeadHeroDamageMin()
	{
		return 2 + _leadHero.Strength;
	}

	private int GetLeadHeroDamageMax()
	{
		return 5 + _leadHero.Strength * 2;
	}

	private float GetLeadHeroSpeed()
	{
		return 165f + _leadHero.Agility * 5f;
	}

	private float GetLeadHeroAttackCycleScale()
	{
		return Mathf.Clamp(1f - _leadHero.Agility * 0.04f, 0.72f, 1f);
	}

	private float GetLeadHeroCritChance()
	{
		return Mathf.Clamp(0.05f + _leadHero.Agility * 0.025f, 0.05f, 0.35f);
	}

	private float GetLeadHeroCritMultiplier()
	{
		return 1.5f + _leadHero.Strength * 0.12f;
	}

	private float GetLeadHeroSearchSpeedMultiplier()
	{
		return 1f + _leadHero.Agility * 0.12f;
	}

	private Vector2I GetLeadHeroBackpackBlockSize()
	{
		if (_leadHero.Strength >= 6)
		{
			return new Vector2I(3, 4);
		}

		if (_leadHero.Strength >= 4)
		{
			return new Vector2I(3, 3);
		}

		if (_leadHero.Strength >= 2)
		{
			return new Vector2I(2, 3);
		}

		return new Vector2I(2, 2);
	}

	private int GetLeadHeroBackpackCells()
	{
		Vector2I size = GetLeadHeroBackpackBlockSize();
		return size.X * size.Y;
	}

	private int GetLeadHeroSoldierLimit()
	{
		return 2 + _leadHero.Charm * 2;
	}

	private int GetLeadHeroSecondaryHeroLimit()
	{
		return Mathf.Max(0, (_leadHero.Charm - 2) / 2);
	}

	private int GetLeadHeroRecruitCost()
	{
		float discountScale = Mathf.Clamp(1f - _leadHero.Charm * 0.05f, 0.65f, 1f);
		return Mathf.Max(10, Mathf.RoundToInt(RecruitCost * discountScale));
	}

	private int GetLeadHeroSoldierHpBonus()
	{
		int bonus = _leadHero.Charm;
		if (_leadHeroCommandBuffTurns > 0)
		{
			bonus += 2;
		}

		return bonus;
	}

	private int GetLeadHeroSoldierDamageBonus()
	{
		int bonus = _leadHero.Charm >= 2 ? 1 + (_leadHero.Charm - 2) / 3 : 0;
		if (_leadHeroCommandBuffTurns > 0)
		{
			bonus += 1;
		}
		return bonus;
	}

	private int GetLeadHeroStrengthValue()
	{
		return 3 + (_leadHero.Level - 1) + _leadHero.Strength + _leadHero.Charm;
	}

	private string GetHeroAttributeLabel(HeroAttribute attribute) => attribute switch
	{
		HeroAttribute.Strength => "力量",
		HeroAttribute.Agility => "敏捷",
		HeroAttribute.Intelligence => "智力",
		HeroAttribute.Charm => "魅力",
		_ => "属性",
	};

	private int GetHeroAttributeValue(HeroAttribute attribute) => attribute switch
	{
		HeroAttribute.Strength => _leadHero.Strength,
		HeroAttribute.Agility => _leadHero.Agility,
		HeroAttribute.Intelligence => _leadHero.Intelligence,
		HeroAttribute.Charm => _leadHero.Charm,
		_ => 0,
	};

	private void SpendLeadHeroAttributePoint(HeroAttribute attribute)
	{
		if (_leadHero.UnspentStatPoints <= 0)
		{
			return;
		}

		switch (attribute)
		{
			case HeroAttribute.Strength:
				_leadHero.Strength++;
				break;
			case HeroAttribute.Agility:
				_leadHero.Agility++;
				break;
			case HeroAttribute.Intelligence:
				_leadHero.Intelligence++;
				break;
			case HeroAttribute.Charm:
				_leadHero.Charm++;
				break;
		}

		_leadHero.UnspentStatPoints--;
		_playerMaxHp = GetLeadHeroMaxHp();
		_playerHp = _playerMaxHp;
		RecalculatePlayerStrength();
		_status = $"{_leadHero.Name} 的{GetHeroAttributeLabel(attribute)}提升到了 {GetHeroAttributeValue(attribute)}。";
	}

	private void GrantLeadHeroExperience(int amount, string reason)
	{
		if (amount <= 0)
		{
			return;
		}

		_leadHero.Experience += amount;
		int levelUps = 0;
		while (_leadHero.Experience >= GetLeadHeroExperienceToNextLevel())
		{
			_leadHero.Experience -= GetLeadHeroExperienceToNextLevel();
			_leadHero.Level++;
			_leadHero.UnspentStatPoints += 2;
			_leadHero.UnspentSkillPoints += 1;
			levelUps++;
		}

		RecalculatePlayerStrength();
		if (levelUps > 0)
		{
			_status = $"{_leadHero.Name} 升到 {_leadHero.Level} 级，获得 {levelUps * 2} 点属性点和 {levelUps} 点技能点。";
		}

		LogEvent($"{_leadHero.Name} 因{reason}获得 {amount} 点经验。");
	}

	private void ApplyLeadHeroStatsToRoomUnit(RoomUnit hero)
	{
		hero.MaxHp = GetLeadHeroMaxHp();
		hero.Hp = Mathf.Clamp(_playerHp, 1, hero.MaxHp);
		hero.DamageMin = GetLeadHeroDamageMin();
		hero.DamageMax = GetLeadHeroDamageMax();
		hero.Speed = GetLeadHeroSpeed();
		hero.AttackCycleScale = GetLeadHeroAttackCycleScale();
	}

	private int ApplyLeadHeroCriticalStrike(RoomUnit attacker, int damage, ref bool heavy)
	{
		if (!attacker.IsHero || _rng.Randf() >= GetLeadHeroCritChance())
		{
			return damage;
		}

		heavy = true;
		return Mathf.Max(damage + 1, Mathf.RoundToInt(damage * GetLeadHeroCritMultiplier()));
	}

	private float GetLeadHeroEvasiveSpeedMultiplier(RoomUnit unit)
	{
		if (!_leadHero.LearnedAgilitySkill || unit == null || !unit.IsHero || unit.HeroEvasiveTime <= 0f)
		{
			return 1f;
		}

		return 1.28f;
	}

	private void TriggerLeadHeroMomentum(RoomUnit unit)
	{
		if (_leadHero.LearnedStrengthSkill && unit != null && unit.IsHero)
		{
			unit.HeroMomentumTime = 8f;
		}
	}

	private void TriggerLeadHeroEvasiveStep(RoomUnit unit)
	{
		if (_leadHero.LearnedAgilitySkill && unit != null && unit.IsHero)
		{
			unit.HeroEvasiveTime = 2.25f;
		}
	}

	private bool HasLearnedHeroSkill(HeroSkill skill) => skill switch
	{
		HeroSkill.StrengthCore => _leadHero.LearnedStrengthSkill,
		HeroSkill.AgilityCore => _leadHero.LearnedAgilitySkill,
		HeroSkill.IntelligenceCore => _leadHero.LearnedIntelligenceSkill,
		HeroSkill.CharmCore => _leadHero.LearnedCharmSkill,
		_ => false,
	};

	private string GetHeroSkillName(HeroSkill skill) => skill switch
	{
		HeroSkill.StrengthCore => "力量系：猛攻余势",
		HeroSkill.AgilityCore => "敏捷系：闪身脱离",
		HeroSkill.IntelligenceCore => "智力系：战地急救",
		HeroSkill.CharmCore => "魅力系：鼓舞号令",
		_ => "技能",
	};

	private string GetHeroSkillEffectSummary(HeroSkill skill) => skill switch
	{
		HeroSkill.StrengthCore => "已生效：近战击杀后，下一次近战攻击强化。",
		HeroSkill.AgilityCore => "已生效：主英雄受击后短时间加速脱离。",
		HeroSkill.IntelligenceCore => "已生效：清图后按急救点池治疗最危险单位。",
		HeroSkill.CharmCore => "已生效：清图后获得数回合团队强化。",
		_ => "占位",
	};

	private void LearnHeroSkill(HeroSkill skill)
	{
		if (_leadHero.UnspentSkillPoints <= 0 || HasLearnedHeroSkill(skill))
		{
			return;
		}

		switch (skill)
		{
			case HeroSkill.StrengthCore:
				_leadHero.LearnedStrengthSkill = true;
				break;
			case HeroSkill.AgilityCore:
				_leadHero.LearnedAgilitySkill = true;
				break;
			case HeroSkill.IntelligenceCore:
				_leadHero.LearnedIntelligenceSkill = true;
				break;
			case HeroSkill.CharmCore:
				_leadHero.LearnedCharmSkill = true;
				break;
		}

		_leadHero.UnspentSkillPoints--;
		_playerMaxHp = GetLeadHeroMaxHp();
		_playerHp = Mathf.Min(_playerHp, _playerMaxHp);
		RecalculatePlayerStrength();
		_status = $"{_leadHero.Name} 学会了 {GetHeroSkillName(skill)}。";
	}

	private int GetLeadHeroFieldMedicinePoints()
	{
		if (!_leadHero.LearnedIntelligenceSkill)
		{
			return 0;
		}

		return 2 + _leadHero.Intelligence * 2;
	}

	private void ApplyLeadHeroRoomClearSupport()
	{
		int points = GetLeadHeroFieldMedicinePoints();
		if (points <= 0)
		{
			return;
		}

		while (points > 0)
		{
			RoomUnit target = FindLowestHealthPercentPlayerUnit();
			if (target == null)
			{
				break;
			}

			target.Hp = Mathf.Min(target.MaxHp, target.Hp + 1);
			if (target.IsHero)
			{
				_playerHp = Mathf.Min(_playerMaxHp, _playerHp + 1);
			}

			points--;
		}
	}

	private RoomUnit FindLowestHealthPercentPlayerUnit()
	{
		RoomUnit best = null;
		float bestRatio = float.MaxValue;
		for (int i = 0; i < _roomUnits.Count; i++)
		{
			RoomUnit unit = _roomUnits[i];
			if (!unit.IsAlive || !unit.IsPlayerSide || unit.MaxHp <= 0 || unit.Hp >= unit.MaxHp)
			{
				continue;
			}

			float ratio = unit.Hp / (float)unit.MaxHp;
			if (ratio < bestRatio)
			{
				bestRatio = ratio;
				best = unit;
			}
		}

		return best;
	}

	private void ApplyLeadHeroCommandBuff()
	{
		if (_leadHero.LearnedCharmSkill)
		{
			_leadHeroCommandBuffTurns = Mathf.Max(_leadHeroCommandBuffTurns, 4);
		}
	}

	private void DrawLeadHeroPanel(Rect2 rect)
	{
		DrawRect(rect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(rect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(18f)), "主英雄", HorizontalAlignment.Left, -1f, UiFont(14), Colors.White);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(34f)), $"{_leadHero.Name}  Lv {_leadHero.Level}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(11), new Color(0.96f, 0.9f, 0.78f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(48f)), $"XP {_leadHero.Experience}/{GetLeadHeroExperienceToNextLevel()}  属性点 {_leadHero.UnspentStatPoints}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.82f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(61f)), $"技能点 {_leadHero.UnspentSkillPoints}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), _leadHero.UnspentSkillPoints > 0 ? new Color(0.98f, 0.84f, 0.46f) : new Color(0.82f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(74f)), $"HP {GetLeadHeroMaxHp()}  伤害 {GetLeadHeroDamageMin()}-{GetLeadHeroDamageMax()}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(87f)), $"暴击 {Mathf.RoundToInt(GetLeadHeroCritChance() * 100f)}%  x{GetLeadHeroCritMultiplier():0.00}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(100f)), $"移速 {Mathf.RoundToInt(GetLeadHeroSpeed())}  搜索 x{GetLeadHeroSearchSpeedMultiplier():0.00}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(113f)), $"背包 {GetLeadHeroBackpackCells()}  士兵上限 {GetLeadHeroSoldierLimit()}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));

		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(126f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Strength, "hero_add_strength");
		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(78f), Ui(126f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Agility, "hero_add_agility");
		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(140f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Intelligence, "hero_add_intelligence");
		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(78f), Ui(140f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Charm, "hero_add_charm");
	}

	private void DrawHeroAttributeControl(Rect2 rect, HeroAttribute attribute, string action)
	{
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(0f, Ui(12f)), $"{GetHeroAttributeLabel(attribute)} {GetHeroAttributeValue(attribute)}", HorizontalAlignment.Left, Ui(48f), UiFont(9), Colors.White);
		Rect2 addRect = new(rect.End - new Vector2(Ui(18f), Ui(16f)), new Vector2(Ui(18f), Ui(16f)));
		bool canAdd = _leadHero.UnspentStatPoints > 0;
		DrawButton(addRect, "+", canAdd ? new Color(0.28f, 0.5f, 0.3f) : new Color(0.24f, 0.24f, 0.28f));
		if (canAdd)
		{
			_buttons.Add(new ButtonDef(addRect, action));
		}
	}

	private void DrawLeadHeroSkillPanel(Rect2 rect)
	{
		DrawRect(rect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(rect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(18f)), "技能骨架", HorizontalAlignment.Left, -1f, UiFont(14), Colors.White);
		DrawHeroSkillRow(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(26f)), new Vector2(rect.Size.X - Ui(20f), Ui(28f))), HeroSkill.StrengthCore, "learn_strength_skill", "占位：破甲路线 / 重武器路线");
		DrawHeroSkillRow(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(54f)), new Vector2(rect.Size.X - Ui(20f), Ui(28f))), HeroSkill.AgilityCore, "learn_agility_skill", "占位：翻滚路线 / 快射路线");
		DrawHeroSkillRow(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(82f)), new Vector2(rect.Size.X - Ui(20f), Ui(28f))), HeroSkill.IntelligenceCore, "learn_intelligence_skill", "占位：侦察路线 / 后勤路线");
		DrawHeroSkillRow(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(110f)), new Vector2(rect.Size.X - Ui(20f), Ui(28f))), HeroSkill.CharmCore, "learn_charm_skill", "占位：鼓舞路线 / 统军路线");
	}

	private void DrawHeroSkillRow(Rect2 rect, HeroSkill skill, string action, string placeholderText)
	{
		bool learned = HasLearnedHeroSkill(skill);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(0f, Ui(11f)), GetHeroSkillName(skill), HorizontalAlignment.Left, rect.Size.X - Ui(54f), UiFont(9), learned ? new Color(0.92f, 0.96f, 0.82f) : Colors.White);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(0f, Ui(22f)), learned ? GetHeroSkillEffectSummary(skill) : placeholderText, HorizontalAlignment.Left, rect.Size.X - Ui(54f), UiFont(8), new Color(0.76f, 0.82f, 0.88f));
		Rect2 buttonRect = new(rect.End - new Vector2(Ui(44f), Ui(24f)), new Vector2(Ui(44f), Ui(22f)));
		bool canLearn = !learned && _leadHero.UnspentSkillPoints > 0;
		string label = learned ? "已学" : "学习";
		DrawButton(buttonRect, label, learned ? new Color(0.24f, 0.4f, 0.24f) : (canLearn ? new Color(0.34f, 0.34f, 0.18f) : new Color(0.24f, 0.24f, 0.28f)));
		if (canLearn)
		{
			_buttons.Add(new ButtonDef(buttonRect, action));
		}
	}
}
