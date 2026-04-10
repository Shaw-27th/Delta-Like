using Godot;
using System.Collections.Generic;

public partial class SlashPreviewDemo : Node2D
{
	private sealed class SlashStyle
	{
		public string Name = "";
		public string Note = "";
		public float Length;
		public float Angle;
		public float MaxWidth;
		public float StartDot;
		public float EndDot;
		public int Ghosts;
		public float GhostStep;
		public float Curve;
		public float Taper;
		public float CoreWidth;
		public float Bloom;
		public bool Reverse;
		public bool Twin;
		public bool Pulse;
		public bool Sway;
		public bool SharpEnd;
		public Color BaseColor;
	}

	private readonly List<SlashStyle> _styles = new();
	private readonly List<Rect2> _cards = new();
	private float _time;
	private int _selectedIndex;

	public override void _Ready()
	{
		BuildStyles();
	}

	public override void _Process(double delta)
	{
		_time += (float)delta;
		QueueRedraw();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
		{
			for (int i = 0; i < _cards.Count; i++)
			{
				if (_cards[i].HasPoint(mouse.Position))
				{
					_selectedIndex = i;
					QueueRedraw();
					return;
				}
			}
		}

		if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode >= Key.Key1 && key.Keycode <= Key.Key9)
			{
				int index = (int)(key.Keycode - Key.Key1);
				if (index < _styles.Count)
				{
					_selectedIndex = index;
				}
			}
			else if (key.Keycode == Key.Key0 && _styles.Count >= 10)
			{
				_selectedIndex = 9;
			}
			else if (key.Keycode == Key.Left)
			{
				_selectedIndex = Mathf.PosMod(_selectedIndex - 1, _styles.Count);
			}
			else if (key.Keycode == Key.Right)
			{
				_selectedIndex = (_selectedIndex + 1) % _styles.Count;
			}

			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		Vector2 viewport = GetViewportRect().Size;
		DrawRect(new Rect2(Vector2.Zero, viewport), new Color(0.045f, 0.05f, 0.06f), true);
		DrawBackdrop(viewport);
		DrawHeader(viewport);
		DrawCards(viewport);
	}

	private void DrawBackdrop(Vector2 viewport)
	{
		for (int i = 0; i < 7; i++)
		{
			float t = i / 6f;
			Rect2 band = new(new Vector2(0f, 90f + i * 92f), new Vector2(viewport.X, 1f));
			DrawRect(band, new Color(0.12f + t * 0.03f, 0.14f + t * 0.03f, 0.18f + t * 0.04f, 0.12f), true);
		}
	}

	private void DrawHeader(Vector2 viewport)
	{
		Vector2 titlePos = new(40f, 50f);
		DrawString(ThemeDB.FallbackFont, titlePos, "斩击光带预览", HorizontalAlignment.Left, -1f, 28, Colors.White);
		DrawString(ThemeDB.FallbackFont, titlePos + new Vector2(0f, 28f), "这次只看一种家族：起点亮点 -> 拉成斩击光带 -> 收束到终点亮点。", HorizontalAlignment.Left, -1f, 14, new Color(0.78f, 0.84f, 0.92f));
		DrawString(ThemeDB.FallbackFont, new Vector2(40f, 114f), "点击卡片或按数字键 1-0。告诉我编号，我再把正式战斗往那个方向收。", HorizontalAlignment.Left, -1f, 13, new Color(0.92f, 0.82f, 0.58f));

		SlashStyle selected = _styles[_selectedIndex];
		Rect2 badge = new(new Vector2(viewport.X - 380f, 34f), new Vector2(332f, 88f));
		DrawRect(badge, new Color(0.08f, 0.09f, 0.11f, 0.94f), true);
		DrawRect(badge, selected.BaseColor.Lightened(0.35f), false, 1.8f);
		DrawString(ThemeDB.FallbackFont, badge.Position + new Vector2(16f, 28f), $"当前选中 #{_selectedIndex + 1}", HorizontalAlignment.Left, -1f, 14, new Color(0.88f, 0.92f, 0.97f));
		DrawString(ThemeDB.FallbackFont, badge.Position + new Vector2(16f, 54f), selected.Name, HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, badge.Position + new Vector2(16f, 74f), selected.Note, HorizontalAlignment.Left, -1f, 12, selected.BaseColor.Lightened(0.28f));
	}

	private void DrawCards(Vector2 viewport)
	{
		_cards.Clear();
		float top = 150f;
		float side = 34f;
		float gap = 18f;
		float cardWidth = (viewport.X - side * 2f - gap * 4f) / 5f;
		float cardHeight = 250f;

		for (int i = 0; i < _styles.Count; i++)
		{
			int col = i % 5;
			int row = i / 5;
			Rect2 card = new(
				new Vector2(side + col * (cardWidth + gap), top + row * (cardHeight + gap)),
				new Vector2(cardWidth, cardHeight));
			_cards.Add(card);
			DrawCard(card, i, _styles[i], i == _selectedIndex);
		}
	}

	private void DrawCard(Rect2 card, int index, SlashStyle style, bool selected)
	{
		DrawRect(card, selected ? new Color(0.095f, 0.11f, 0.13f, 0.98f) : new Color(0.075f, 0.082f, 0.095f, 0.96f), true);
		DrawRect(card, selected ? style.BaseColor.Lightened(0.36f) : new Color(0.26f, 0.28f, 0.32f), false, selected ? 2.3f : 1.2f);
		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(14f, 28f), $"#{index + 1} {style.Name}", HorizontalAlignment.Left, card.Size.X - 28f, 16, Colors.White);
		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(14f, 48f), style.Note, HorizontalAlignment.Left, card.Size.X - 28f, 11, style.BaseColor.Lightened(0.28f));

		Rect2 stage = new(card.Position + new Vector2(12f, 64f), new Vector2(card.Size.X - 24f, 132f));
		DrawRect(stage, new Color(0.05f, 0.06f, 0.075f, 0.98f), true);
		DrawRect(stage, new Color(0.17f, 0.19f, 0.22f), false, 1.1f);
		DrawStageGrid(stage);
		DrawSlashPreview(stage, style);

		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(14f, card.Size.Y - 30f), "只选视觉节奏和形态。", HorizontalAlignment.Left, card.Size.X - 28f, 11, new Color(0.72f, 0.76f, 0.82f));
	}

	private void DrawStageGrid(Rect2 stage)
	{
		for (float x = stage.Position.X + 16f; x < stage.End.X; x += 16f)
		{
			DrawLine(new Vector2(x, stage.Position.Y), new Vector2(x, stage.End.Y), new Color(0.09f, 0.1f, 0.12f, 0.34f), 1f);
		}
		for (float y = stage.Position.Y + 16f; y < stage.End.Y; y += 16f)
		{
			DrawLine(new Vector2(stage.Position.X, y), new Vector2(stage.End.X, y), new Color(0.09f, 0.1f, 0.12f, 0.34f), 1f);
		}
	}

	private void DrawSlashPreview(Rect2 stage, SlashStyle style)
	{
		float cycle = Mathf.PosMod(_time * 0.78f, 1.6f) / 1.6f;
		float progress = Mathf.SmoothStep(0f, 1f, cycle);
		Vector2 start = stage.GetCenter() + new Vector2(-48f, 8f);
		DrawActor(start);
		DrawTargetMarks(stage);
		RenderSlashBand(start, style, progress);
		if (style.Twin)
		{
			SlashStyle twin = CreateTwin(style);
			RenderSlashBand(start + new Vector2(0f, -6f), twin, Mathf.PosMod(progress + 0.13f, 1f));
		}
	}

	private void DrawActor(Vector2 start)
	{
		DrawCircle(start, 8f, new Color(0.32f, 0.78f, 0.98f));
		DrawRect(new Rect2(start + new Vector2(-6f, 8f), new Vector2(12f, 22f)), new Color(0.24f, 0.42f, 0.52f), true);
		DrawLine(start + new Vector2(2f, 14f), start + new Vector2(20f, 4f), new Color(0.64f, 0.84f, 0.98f), 2f);
	}

	private void DrawTargetMarks(Rect2 stage)
	{
		Vector2[] points =
		[
			stage.GetCenter() + new Vector2(46f, -18f),
			stage.GetCenter() + new Vector2(68f, 8f),
			stage.GetCenter() + new Vector2(54f, 30f),
		];
		for (int i = 0; i < points.Length; i++)
		{
			DrawCircle(points[i], 5f, new Color(0.86f, 0.36f, 0.34f));
			DrawRect(new Rect2(points[i] + new Vector2(-4f, 5f), new Vector2(8f, 16f)), new Color(0.45f, 0.19f, 0.18f), true);
		}
	}

	private void RenderSlashBand(Vector2 start, SlashStyle style, float progress)
	{
		Vector2 direction = Vector2.Right.Rotated(style.Angle);
		if (style.Reverse)
		{
			direction = direction.Rotated(Mathf.Pi);
		}

		Vector2 normal = new(-direction.Y, direction.X);
		float sway = style.Sway ? Mathf.Sin(progress * Mathf.Pi) * style.Curve : style.Curve * 0.35f;
		Vector2 end = start + direction * style.Length + normal * sway;

		float reveal = Mathf.Clamp(progress / 0.48f, 0f, 1f);
		float collapse = Mathf.Clamp((progress - 0.58f) / 0.42f, 0f, 1f);
		float liveStartT = collapse;
		float liveEndT = reveal;
		float lineWidth = Mathf.Lerp(0f, style.MaxWidth, reveal) * (1f - collapse * style.Taper);
		float coreWidth = Mathf.Lerp(0f, style.CoreWidth, reveal) * (1f - collapse * style.Taper * 0.85f);
		float alpha = style.Pulse ? 0.8f + Mathf.Sin(progress * Mathf.Pi) * 0.28f : 1f;
		Vector2 control = (start + end) * 0.5f + normal * style.Curve;

		if (style.Ghosts > 0 && lineWidth > 0.3f)
		{
			for (int i = style.Ghosts - 1; i >= 0; i--)
			{
				float t = i / Mathf.Max(1f, style.Ghosts - 1f);
				Vector2 ghostOffset = normal * (i - (style.Ghosts - 1) * 0.5f) * style.GhostStep;
				float ghostWidth = Mathf.Max(0.4f, lineWidth - t * 1.1f);
				Color ghostColor = new(style.BaseColor.R, style.BaseColor.G, style.BaseColor.B, alpha * (0.34f - t * 0.18f));
				DrawCurvedSlashStroke(start + ghostOffset, control + ghostOffset, end + ghostOffset, liveStartT, liveEndT, ghostWidth, ghostColor, style.SharpEnd);
			}
		}

		if (style.Bloom > 0f && lineWidth > 0.3f)
		{
			Color bloomColor = new(style.BaseColor.R, style.BaseColor.G, style.BaseColor.B, alpha * 0.22f);
			DrawCurvedSlashStroke(start, control, end, liveStartT, liveEndT, lineWidth + style.Bloom, bloomColor, style.SharpEnd);
		}

		if (lineWidth > 0.2f)
		{
			Color body = new(style.BaseColor.R, style.BaseColor.G, style.BaseColor.B, alpha * 0.92f);
			DrawCurvedSlashStroke(start, control, end, liveStartT, liveEndT, lineWidth, body, style.SharpEnd);
		}

		if (coreWidth > 0.15f)
		{
			DrawCurvedSlashStroke(start, control, end, liveStartT, liveEndT, coreWidth, new Color(1f, 1f, 1f, alpha * 0.88f), style.SharpEnd);
		}

		float startDot = Mathf.Lerp(style.StartDot, 0f, Mathf.Clamp(progress / 0.32f, 0f, 1f));
		if (startDot > 0.25f)
		{
			DrawCircle(start, startDot, new Color(1f, 1f, 1f, 0.96f));
			DrawCircle(start, startDot + 2f, new Color(style.BaseColor.R, style.BaseColor.G, style.BaseColor.B, 0.26f));
		}

		float endDotGrow = Mathf.Clamp((progress - 0.78f) / 0.22f, 0f, 1f);
		float endDot = Mathf.Lerp(0f, style.EndDot, endDotGrow);
		if (endDot > 0.2f)
		{
			DrawCircle(end, endDot + 2f, new Color(style.BaseColor.R, style.BaseColor.G, style.BaseColor.B, 0.22f + endDotGrow * 0.12f));
			DrawCircle(end, endDot, new Color(1f, 1f, 1f, 0.96f));
		}
	}

	private void DrawCurvedSlashStroke(Vector2 start, Vector2 control, Vector2 end, float fromT, float toT, float width, Color color, bool sharpEnd)
	{
		if (toT <= fromT)
		{
			return;
		}

		const int sampleCount = 24;
		Vector2 prev = GetQuadraticPoint(start, control, end, fromT);
		for (int i = 1; i <= sampleCount; i++)
		{
			float t = Mathf.Lerp(fromT, toT, i / (float)sampleCount);
			Vector2 point = GetQuadraticPoint(start, control, end, t);
			float segmentWidth = sharpEnd ? Mathf.Lerp(width, width * 0.22f, i / (float)sampleCount) : width;
			DrawLine(prev, point, color, segmentWidth);
			DrawCircle(point, Mathf.Max(0.4f, segmentWidth * 0.5f), color);
			prev = point;
		}
	}

	private Vector2 GetQuadraticPoint(Vector2 start, Vector2 control, Vector2 end, float t)
	{
		float inv = 1f - t;
		return inv * inv * start + 2f * inv * t * control + t * t * end;
	}

	private SlashStyle CreateTwin(SlashStyle style)
	{
		return new SlashStyle
		{
			Name = style.Name,
			Note = style.Note,
			Length = style.Length * 0.94f,
			Angle = style.Angle - 0.08f,
			MaxWidth = Mathf.Max(1f, style.MaxWidth - 0.5f),
			StartDot = style.StartDot * 0.86f,
			EndDot = style.EndDot * 0.88f,
			Ghosts = Mathf.Max(1, style.Ghosts - 1),
			GhostStep = style.GhostStep,
			Curve = style.Curve * 0.6f,
			Taper = style.Taper,
			CoreWidth = style.CoreWidth * 0.9f,
			Bloom = style.Bloom * 0.9f,
			Reverse = style.Reverse,
			Twin = false,
			Pulse = style.Pulse,
			Sway = style.Sway,
			SharpEnd = style.SharpEnd,
			BaseColor = style.BaseColor.Lightened(0.1f),
		};
	}

	private void BuildStyles()
	{
		_styles.Clear();
		_styles.Add(new SlashStyle { Name = "细锋弧", Note = "细、干净、中弯", Length = 96f, Angle = -0.28f, MaxWidth = 7f, StartDot = 3.8f, EndDot = 4.6f, Ghosts = 3, GhostStep = 1.6f, Curve = -24f, Taper = 0.92f, CoreWidth = 2.2f, Bloom = 3f, SharpEnd = true, BaseColor = new Color(0.78f, 0.96f, 1f) });
		_styles.Add(new SlashStyle { Name = "白刃弧", Note = "中芯亮，弧度更开", Length = 92f, Angle = -0.18f, MaxWidth = 8.5f, StartDot = 4f, EndDot = 5f, Ghosts = 2, GhostStep = 1.2f, Curve = -20f, Taper = 0.88f, CoreWidth = 3f, Bloom = 4f, SharpEnd = true, BaseColor = new Color(0.9f, 0.94f, 1f) });
		_styles.Add(new SlashStyle { Name = "青断弧", Note = "更细更冷，大弧长线", Length = 102f, Angle = -0.35f, MaxWidth = 6.6f, StartDot = 3.4f, EndDot = 4.2f, Ghosts = 4, GhostStep = 1.4f, Curve = -34f, Taper = 0.95f, CoreWidth = 1.8f, Bloom = 2.8f, Pulse = true, SharpEnd = true, BaseColor = new Color(0.72f, 0.94f, 1f) });
		_styles.Add(new SlashStyle { Name = "赤痕弧", Note = "厚一些，重击月弧", Length = 88f, Angle = -0.08f, MaxWidth = 10.5f, StartDot = 4.2f, EndDot = 5.4f, Ghosts = 3, GhostStep = 1.8f, Curve = -18f, Taper = 0.78f, CoreWidth = 2.4f, Bloom = 5f, SharpEnd = true, BaseColor = new Color(1f, 0.78f, 0.72f) });
		_styles.Add(new SlashStyle { Name = "弦月", Note = "大月牙弧度", Length = 98f, Angle = -0.22f, MaxWidth = 7.6f, StartDot = 3.6f, EndDot = 4.8f, Ghosts = 3, GhostStep = 1.4f, Curve = -44f, Taper = 0.9f, CoreWidth = 2.2f, Bloom = 3.4f, Sway = true, SharpEnd = true, BaseColor = new Color(0.8f, 1f, 0.92f) });
		_styles.Add(new SlashStyle { Name = "双月", Note = "双层大弧残影", Length = 94f, Angle = -0.24f, MaxWidth = 7.2f, StartDot = 3.8f, EndDot = 4.5f, Ghosts = 2, GhostStep = 1.2f, Curve = -30f, Taper = 0.92f, CoreWidth = 2.1f, Bloom = 3f, Twin = true, SharpEnd = true, BaseColor = new Color(0.78f, 1f, 0.98f) });
		_styles.Add(new SlashStyle { Name = "拖影弧", Note = "尾迹更明显，大弯", Length = 100f, Angle = -0.3f, MaxWidth = 8f, StartDot = 3.4f, EndDot = 4.8f, Ghosts = 5, GhostStep = 1.9f, Curve = -32f, Taper = 0.93f, CoreWidth = 2f, Bloom = 3.2f, Pulse = true, SharpEnd = true, BaseColor = new Color(0.84f, 0.96f, 1f) });
		_styles.Add(new SlashStyle { Name = "逆收弧", Note = "终点收束更狠，弧更圆", Length = 90f, Angle = -0.12f, MaxWidth = 8.8f, StartDot = 4.2f, EndDot = 6.2f, Ghosts = 3, GhostStep = 1.4f, Curve = -24f, Taper = 1.18f, CoreWidth = 2.4f, Bloom = 4.2f, SharpEnd = true, BaseColor = new Color(1f, 0.86f, 0.8f) });
		_styles.Add(new SlashStyle { Name = "斜月切", Note = "斜切感更强，弧也大", Length = 108f, Angle = -0.48f, MaxWidth = 7.4f, StartDot = 3.4f, EndDot = 4.7f, Ghosts = 4, GhostStep = 1.5f, Curve = -38f, Taper = 0.9f, CoreWidth = 2.1f, Bloom = 3.2f, SharpEnd = true, BaseColor = new Color(0.78f, 0.94f, 1f) });
		_styles.Add(new SlashStyle { Name = "收束月白", Note = "白点和大月弧最明显", Length = 92f, Angle = -0.2f, MaxWidth = 7.8f, StartDot = 3.4f, EndDot = 7.2f, Ghosts = 2, GhostStep = 1.1f, Curve = -42f, Taper = 1.06f, CoreWidth = 2.8f, Bloom = 4.8f, Pulse = true, SharpEnd = true, BaseColor = new Color(0.9f, 0.96f, 1f) });
	}
}
