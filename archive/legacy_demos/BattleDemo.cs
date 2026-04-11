using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class BattleDemo : Node2D
{
	private const int AllyMaxHp = 10;
	private const int AllyMaxAmmo = 8;
	private const float AllyMoveSpeed = 165.0f;
	private const float AllyShotRange = 360.0f;

	private const int EnemyMaxHp = 10;
	private const int EnemyMaxAmmo = 8;
	private const float EnemyMoveSpeed = 165.0f;
	private const float EnemyShotRange = 360.0f;

	private sealed class ShotLine
	{
		public Vector2 From;
		public Vector2 To;
		public Color Color;
		public float TimeLeft;
		public bool IsMelee;
	}

	private readonly List<ShotLine> _shots = new();
	private AutoUnit _ally;
	private AutoUnit _enemy;
	private Rect2[] _obstacles;

	public override void _Ready()
	{
		Rect2 arenaRect = new(new Vector2(90.0f, 90.0f), new Vector2(970.0f, 470.0f));
		_obstacles =
		[
			new Rect2(new Vector2(360.0f, 170.0f), new Vector2(90.0f, 150.0f)),
			new Rect2(new Vector2(705.0f, 330.0f), new Vector2(90.0f, 150.0f)),
			new Rect2(new Vector2(515.0f, 250.0f), new Vector2(120.0f, 60.0f)),
		];

		_ally = CreateUnit(
			name: "Ally",
			displayName: "Ally",
			color: new Color(0.2f, 0.85f, 1.0f),
			position: new Vector2(260.0f, 325.0f),
			facingAngle: 0.0f,
			arenaRect: arenaRect,
			maxHp: AllyMaxHp,
			maxAmmo: AllyMaxAmmo,
			moveSpeed: AllyMoveSpeed,
			shotRange: AllyShotRange);

		_enemy = CreateUnit(
			name: "Enemy",
			displayName: "Enemy",
			color: new Color(1.0f, 0.35f, 0.35f),
			position: new Vector2(890.0f, 325.0f),
			facingAngle: Mathf.Pi,
			arenaRect: arenaRect,
			maxHp: EnemyMaxHp,
			maxAmmo: EnemyMaxAmmo,
			moveSpeed: EnemyMoveSpeed,
			shotRange: EnemyShotRange);

		_ally.Target = _enemy;
		_enemy.Target = _ally;
		QueueRedraw();
	}

	public override void _Process(double delta)
	{
		for (int i = _shots.Count - 1; i >= 0; i--)
		{
			_shots[i].TimeLeft -= (float)delta;
			if (_shots[i].TimeLeft <= 0.0f)
			{
				_shots.RemoveAt(i);
			}
		}

		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 viewportSize = GetViewportRect().Size;
		DrawRect(new Rect2(Vector2.Zero, viewportSize), new Color(0.07f, 0.08f, 0.11f), true);

		Rect2 arenaRect = new(new Vector2(90.0f, 90.0f), new Vector2(970.0f, 470.0f));
		DrawRect(arenaRect, new Color(0.11f, 0.13f, 0.17f), true);
		DrawRect(arenaRect, new Color(0.28f, 0.31f, 0.38f), false, 3.0f);

		for (float x = arenaRect.Position.X + 80.0f; x < arenaRect.End.X; x += 80.0f)
		{
			DrawLine(new Vector2(x, arenaRect.Position.Y), new Vector2(x, arenaRect.End.Y), new Color(0.16f, 0.18f, 0.22f), 1.0f);
		}

		for (float y = arenaRect.Position.Y + 80.0f; y < arenaRect.End.Y; y += 80.0f)
		{
			DrawLine(new Vector2(arenaRect.Position.X, y), new Vector2(arenaRect.End.X, y), new Color(0.16f, 0.18f, 0.22f), 1.0f);
		}

		foreach (Rect2 obstacle in _obstacles)
		{
			DrawRect(obstacle, new Color(0.28f, 0.3f, 0.34f), true);
			DrawRect(obstacle, new Color(0.78f, 0.82f, 0.9f), false, 2.0f);
			Vector2 center = obstacle.GetCenter();
			DrawLine(
				new Vector2(obstacle.Position.X + 10.0f, center.Y),
				new Vector2(obstacle.End.X - 10.0f, center.Y),
				new Color(0.88f, 0.92f, 0.98f, 0.4f),
				2.0f);
		}

		foreach (ShotLine shot in _shots)
		{
			float duration = shot.IsMelee ? 0.22f : 0.16f;
			float alpha = Mathf.Clamp(shot.TimeLeft / duration, 0.0f, 1.0f);
			Color shotColor = shot.Color;
			shotColor.A = alpha;
			DrawLine(shot.From, shot.To, shotColor, shot.IsMelee ? 7.0f : 4.0f);
		}

		if (_ally != null && _enemy != null && (!_ally.IsAlive || !_enemy.IsAlive))
		{
			DrawVictoryBanner(viewportSize);
		}
	}

	private AutoUnit CreateUnit(
		string name,
		string displayName,
		Color color,
		Vector2 position,
		float facingAngle,
		Rect2 arenaRect,
		int maxHp,
		int maxAmmo,
		float moveSpeed,
		float shotRange)
	{
		AutoUnit unit = new()
		{
			Name = name,
			DisplayName = displayName,
			UnitColor = color,
			Position = position,
			ArenaRect = arenaRect,
			Obstacles = _obstacles,
			FacingAngle = facingAngle,
			MaxHp = maxHp,
			MaxAmmo = maxAmmo,
			MaxMoveSpeed = moveSpeed,
			ShotRange = shotRange,
		};

		unit.Fired += OnUnitFired;
		AddChild(unit);
		return unit;
	}

	private void OnUnitFired(Vector2 from, Vector2 to, Color color, bool hit, bool isMelee)
	{
		Color shotColor = hit ? color.Lightened(0.35f) : color.Darkened(0.15f);
		if (isMelee)
		{
			shotColor = shotColor.Lightened(0.15f);
		}

		_shots.Add(new ShotLine
		{
			From = from,
			To = to,
			Color = shotColor,
			TimeLeft = isMelee ? 0.22f : (hit ? 0.2f : 0.14f),
			IsMelee = isMelee,
		});
	}

	private void DrawVictoryBanner(Vector2 viewportSize)
	{
		string winner = _ally.IsAlive ? "Ally Wins" : "Enemy Wins";
		Vector2 center = viewportSize * 0.5f;
		Rect2 panel = new(center - new Vector2(120.0f, 30.0f), new Vector2(240.0f, 60.0f));
		DrawRect(panel, new Color(0.05f, 0.05f, 0.07f, 0.88f), true);
		DrawRect(panel, Colors.White, false, 2.0f);
		DrawString(ThemeDB.FallbackFont, center + new Vector2(-68.0f, 10.0f), winner, HorizontalAlignment.Left, -1.0f, 24, Colors.White);
	}
}
