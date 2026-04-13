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
		return _leadHero.Charm;
	}

	private int GetLeadHeroSoldierDamageBonus()
	{
		return _leadHero.Charm >= 2 ? 1 + (_leadHero.Charm - 2) / 3 : 0;
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
			levelUps++;
		}

		RecalculatePlayerStrength();
		if (levelUps > 0)
		{
			_status = $"{_leadHero.Name} 升到 {_leadHero.Level} 级，获得 {levelUps * 2} 点属性点。";
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

	private void DrawLeadHeroPanel(Rect2 rect)
	{
		DrawRect(rect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(rect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(18f)), "主英雄", HorizontalAlignment.Left, -1f, UiFont(14), Colors.White);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(34f)), $"{_leadHero.Name}  Lv {_leadHero.Level}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(11), new Color(0.96f, 0.9f, 0.78f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(48f)), $"XP {_leadHero.Experience}/{GetLeadHeroExperienceToNextLevel()}  点数 {_leadHero.UnspentStatPoints}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.82f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(61f)), $"HP {GetLeadHeroMaxHp()}  伤害 {GetLeadHeroDamageMin()}-{GetLeadHeroDamageMax()}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(74f)), $"暴击 {Mathf.RoundToInt(GetLeadHeroCritChance() * 100f)}%  x{GetLeadHeroCritMultiplier():0.00}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(87f)), $"移速 {Mathf.RoundToInt(GetLeadHeroSpeed())}  搜索 x{GetLeadHeroSearchSpeedMultiplier():0.00}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(100f)), $"背包 {GetLeadHeroBackpackCells()}  士兵上限 {GetLeadHeroSoldierLimit()}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(Ui(10f), Ui(113f)), $"次英雄位 {GetLeadHeroSecondaryHeroLimit()}  征募价 {GetLeadHeroRecruitCost()}", HorizontalAlignment.Left, rect.Size.X - Ui(20f), UiFont(9), new Color(0.86f, 0.9f, 0.96f));

		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(124f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Strength, "hero_add_strength");
		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(78f), Ui(124f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Agility, "hero_add_agility");
		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(10f), Ui(138f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Intelligence, "hero_add_intelligence");
		DrawHeroAttributeControl(new Rect2(rect.Position + new Vector2(Ui(78f), Ui(138f)), new Vector2(Ui(64f), Ui(16f))), HeroAttribute.Charm, "hero_add_charm");
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
}
