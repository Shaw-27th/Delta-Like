using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class RoomBattleSim : Node2D
{
	[Signal]
	public delegate void BattleFinishedEventHandler(bool victory, int remainingHp, int remainingStrength);

	private sealed class BattleUnit
	{
		public string Name = "";
		public bool IsPlayerSide;
		public bool IsHero;
		public Vector2 Position;
		public int Hp;
		public int MaxHp;
		public float Speed;
		public float Range;
		public int DamageMin;
		public int DamageMax;
		public float Cooldown;
		public Color Color;
		public bool IsAlive => Hp > 0;
	}

	private readonly List<BattleUnit> _units = new();
	private readonly List<string> _log = new();
	private readonly List<Rect2> _controlRects = new();
	private readonly RandomNumberGenerator _rng = new();
	private readonly Rect2 _panelRect = new(new Vector2(120f, 70f), new Vector2(960f, 580f));
	private readonly Rect2 _arenaRect = new(new Vector2(160f, 150f), new Vector2(880f, 360f));

	private string _title = "";
	private bool _finished;
	private bool _paused;
	private float _speedScale = 1f;

	public override void _Ready()
	{
		_rng.Randomize();
	}

	public void Setup(string title, int playerHp, int playerStrength, int enemyPower, bool enemyHasElite, string enemyName)
	{
		_title = title;
		_units.Clear();
		_log.Clear();
		_finished = false;
		_paused = false;
		_speedScale = 1f;

		int allyCount = Mathf.Clamp(2 + playerStrength / 4, 3, 5);
		int enemyCount = Mathf.Clamp(2 + enemyPower / 4, 3, 6);

		AddUnit("英雄", true, true, new Vector2(_arenaRect.Position.X + 90f, _arenaRect.GetCenter().Y), Mathf.Clamp(playerHp, 10, 24), 52f, 45f, 3, 5, new Color(0.3f, 0.82f, 1f));
		for (int i = 0; i < allyCount; i++)
		{
			float y = _arenaRect.Position.Y + 50f + i * 58f;
			AddUnit($"士兵{i + 1}", true, false, new Vector2(_arenaRect.Position.X + 40f, y), 8, 58f, 20f, 1, 3, new Color(0.42f, 0.9f, 0.65f));
		}

		if (enemyHasElite)
		{
			AddUnit(enemyName, false, true, new Vector2(_arenaRect.End.X - 90f, _arenaRect.GetCenter().Y), Mathf.Clamp(enemyPower + 6, 12, 28), 48f, 40f, 3, 5, new Color(0.95f, 0.48f, 0.42f));
		}

		for (int i = 0; i < enemyCount; i++)
		{
			float y = _arenaRect.Position.Y + 40f + i * 52f;
			AddUnit($"敌兵{i + 1}", false, false, new Vector2(_arenaRect.End.X - 40f, y), 7, 56f, 18f, 1, 3, new Color(0.92f, 0.34f, 0.34f));
		}

		PushLog($"在 {_title} 发生战斗。");
		PushLog("双方进入房间自动交战。");
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		if (_finished || _paused)
		{
			return;
		}

		float dt = (float)delta * _speedScale;
		for (int i = 0; i < _units.Count; i++)
		{
			BattleUnit unit = _units[i];
			if (!unit.IsAlive)
			{
				continue;
			}

			unit.Cooldown = Mathf.Max(0f, unit.Cooldown - dt);
			BattleUnit target = FindNearestEnemy(unit);
			if (target == null)
			{
				continue;
			}

			Vector2 toTarget = target.Position - unit.Position;
			float distance = toTarget.Length();
			if (distance > unit.Range)
			{
				Vector2 dir = toTarget.Normalized();
				unit.Position += dir * unit.Speed * dt;
				unit.Position = ClampToArena(unit.Position);
				continue;
			}

			if (unit.Cooldown > 0f)
			{
				continue;
			}

			int damage = _rng.RandiRange(unit.DamageMin, unit.DamageMax) + (unit.IsHero ? 1 : 0);
			target.Hp = Mathf.Max(0, target.Hp - damage);
			unit.Cooldown = unit.IsHero ? 0.52f : 0.7f;
			PushLog($"{unit.Name} 对 {target.Name} 造成 {damage} 点伤害。");
			if (!target.IsAlive)
			{
				PushLog($"{target.Name} 被击倒。");
			}
		}

		QueueRedraw();
		CheckBattleFinished();
	}

	public override void _Draw()
	{
		_controlRects.Clear();
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.55f), true);
		DrawRect(_panelRect, new Color(0.04f, 0.04f, 0.05f, 0.97f), true);
		DrawRect(_panelRect, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, _panelRect.Position + new Vector2(24f, 34f), $"房间战斗：{_title}", HorizontalAlignment.Left, -1f, 24, Colors.White);
		DrawBattleControls();

		DrawRect(_arenaRect, new Color(0.1f, 0.11f, 0.13f), true);
		DrawRect(_arenaRect, new Color(0.28f, 0.31f, 0.35f), false, 2f);
		DrawLine(new Vector2(_arenaRect.GetCenter().X, _arenaRect.Position.Y), new Vector2(_arenaRect.GetCenter().X, _arenaRect.End.Y), new Color(0.2f, 0.22f, 0.26f), 2f);

		foreach (BattleUnit unit in _units)
		{
			if (!unit.IsAlive)
			{
				continue;
			}

			DrawCircle(unit.Position, unit.IsHero ? 16f : 12f, unit.Color);
			DrawArc(unit.Position, unit.IsHero ? 21f : 17f, 0f, Mathf.Tau, 24, Colors.White, 1.5f);
			DrawString(ThemeDB.FallbackFont, unit.Position + new Vector2(-26f, -18f), unit.Name, HorizontalAlignment.Left, -1f, 11, Colors.White);
			DrawRect(new Rect2(unit.Position + new Vector2(-20f, 18f), new Vector2(40f, 5f)), new Color(0.15f, 0.15f, 0.16f), true);
			DrawRect(new Rect2(unit.Position + new Vector2(-20f, 18f), new Vector2(40f * ((float)unit.Hp / unit.MaxHp), 5f)), unit.IsPlayerSide ? new Color(0.42f, 0.94f, 0.58f) : new Color(0.95f, 0.45f, 0.45f), true);
		}

		float logX = _panelRect.Position.X + 24f;
		float logY = _arenaRect.End.Y + 34f;
		DrawString(ThemeDB.FallbackFont, new Vector2(logX, logY), "战斗记录", HorizontalAlignment.Left, -1f, 16, Colors.White);
		logY += 22f;
		for (int i = Mathf.Max(0, _log.Count - 7); i < _log.Count; i++)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(logX, logY), _log[i], HorizontalAlignment.Left, 900f, 13, new Color(0.84f, 0.88f, 0.94f));
			logY += 20f;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouse || !mouse.Pressed || mouse.ButtonIndex != MouseButton.Left)
		{
			return;
		}

		Vector2 click = mouse.Position;
		for (int i = 0; i < _controlRects.Count; i++)
		{
			if (!_controlRects[i].HasPoint(click))
			{
				continue;
			}

			switch (i)
			{
				case 0:
					_paused = !_paused;
					PushLog(_paused ? "战斗已暂停。" : "战斗继续。");
					break;
				case 1:
					_speedScale = 1f;
					PushLog("战斗速度切换为 1x。");
					break;
				case 2:
					_speedScale = 2f;
					PushLog("战斗速度切换为 2x。");
					break;
				case 3:
					_speedScale = 3f;
					PushLog("战斗速度切换为 3x。");
					break;
			}

			QueueRedraw();
			GetViewport().SetInputAsHandled();
			return;
		}
	}

	private void AddUnit(string name, bool isPlayerSide, bool isHero, Vector2 position, int hp, float speed, float range, int damageMin, int damageMax, Color color)
	{
		_units.Add(new BattleUnit
		{
			Name = name,
			IsPlayerSide = isPlayerSide,
			IsHero = isHero,
			Position = position,
			Hp = hp,
			MaxHp = hp,
			Speed = speed,
			Range = range,
			DamageMin = damageMin,
			DamageMax = damageMax,
			Color = color,
		});
	}

	private BattleUnit FindNearestEnemy(BattleUnit from)
	{
		BattleUnit best = null;
		float bestDistance = float.MaxValue;
		foreach (BattleUnit unit in _units)
		{
			if (!unit.IsAlive || unit.IsPlayerSide == from.IsPlayerSide)
			{
				continue;
			}

			float distance = from.Position.DistanceTo(unit.Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = unit;
			}
		}

		return best;
	}

	private Vector2 ClampToArena(Vector2 position)
	{
		return new Vector2(
			Mathf.Clamp(position.X, _arenaRect.Position.X + 16f, _arenaRect.End.X - 16f),
			Mathf.Clamp(position.Y, _arenaRect.Position.Y + 20f, _arenaRect.End.Y - 20f));
	}

	private void PushLog(string text)
	{
		_log.Add(text);
		if (_log.Count > 12)
		{
			_log.RemoveAt(0);
		}
	}

	private void DrawBattleControls()
	{
		float y = _panelRect.Position.Y + 16f;
		float x = _panelRect.End.X - 300f;
		DrawControlButton(new Rect2(new Vector2(x, y), new Vector2(72f, 28f)), _paused ? "继续" : "暂停", 0, _paused ? new Color(0.26f, 0.52f, 0.3f) : new Color(0.45f, 0.24f, 0.2f));
		DrawControlButton(new Rect2(new Vector2(x + 82f, y), new Vector2(56f, 28f)), "1x", 1, Mathf.IsEqualApprox(_speedScale, 1f) ? new Color(0.27f, 0.46f, 0.68f) : new Color(0.16f, 0.18f, 0.22f));
		DrawControlButton(new Rect2(new Vector2(x + 146f, y), new Vector2(56f, 28f)), "2x", 2, Mathf.IsEqualApprox(_speedScale, 2f) ? new Color(0.27f, 0.46f, 0.68f) : new Color(0.16f, 0.18f, 0.22f));
		DrawControlButton(new Rect2(new Vector2(x + 210f, y), new Vector2(56f, 28f)), "3x", 3, Mathf.IsEqualApprox(_speedScale, 3f) ? new Color(0.27f, 0.46f, 0.68f) : new Color(0.16f, 0.18f, 0.22f));
		DrawString(ThemeDB.FallbackFont, new Vector2(_panelRect.End.X - 194f, _panelRect.Position.Y + 60f), _paused ? "状态：暂停" : $"状态：{_speedScale:0}x", HorizontalAlignment.Left, -1f, 13, new Color(0.82f, 0.86f, 0.92f));
	}

	private void DrawControlButton(Rect2 rect, string text, int index, Color fill)
	{
		_controlRects.Add(rect);
		DrawRect(rect, fill, true);
		DrawRect(rect, Colors.White, false, 1.5f);
		DrawString(ThemeDB.FallbackFont, rect.Position + new Vector2(16f, 18f), text, HorizontalAlignment.Left, -1f, 13, Colors.White);
	}

	private void CheckBattleFinished()
	{
		bool playerAlive = false;
		bool enemyAlive = false;
		int heroHp = 0;
		int strength = 0;

		foreach (BattleUnit unit in _units)
		{
			if (!unit.IsAlive)
			{
				continue;
			}

			if (unit.IsPlayerSide)
			{
				playerAlive = true;
				strength += unit.IsHero ? 3 : 1;
				if (unit.IsHero)
				{
					heroHp = unit.Hp;
				}
			}
			else
			{
				enemyAlive = true;
			}
		}

		if (playerAlive && enemyAlive)
		{
			return;
		}

		_finished = true;
		EmitSignal(SignalName.BattleFinished, playerAlive, heroHp, strength);
	}
}
