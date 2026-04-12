using Godot;

[GlobalClass]
public partial class ShieldSoldierPreview : Node2D
{
	private float _time;

	public override void _Process(double delta)
	{
		_time += (float)delta;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 viewport = GetViewportRect().Size;
		DrawRect(new Rect2(Vector2.Zero, viewport), new Color(0.06f, 0.065f, 0.075f), true);
		DrawBackdrop(viewport);
		DrawHeader(viewport);

		int columns = 5;
		float gapX = 220f;
		float gapY = 240f;
		Vector2 start = new(210f, 260f);
		for (int i = 0; i < 10; i++)
		{
			int col = i % columns;
			int row = i / columns;
			Vector2 center = start + new Vector2(col * gapX, row * gapY);
			DrawPreviewCard(center, i);
		}
	}

	private void DrawBackdrop(Vector2 viewport)
	{
		for (int i = 0; i < 6; i++)
		{
			float y = 130f + i * 120f;
			DrawLine(new Vector2(0f, y), new Vector2(viewport.X, y), new Color(0.16f, 0.18f, 0.22f, 0.22f), 1f);
		}
	}

	private void DrawHeader(Vector2 viewport)
	{
		DrawString(ThemeDB.FallbackFont, new Vector2(42f, 52f), "Shield Soldier Preview", HorizontalAlignment.Left, -1f, 28, Colors.White);
		DrawString(ThemeDB.FallbackFont, new Vector2(42f, 82f), "Temporary entry. This scene only shows shield soldiers, fixed at 10 samples for silhouette review.", HorizontalAlignment.Left, viewport.X - 84f, 14, new Color(0.82f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, new Vector2(42f, 106f), "Switch main_scene back to NodeRaidDemo.tscn after the preview pass.", HorizontalAlignment.Left, viewport.X - 84f, 13, new Color(0.95f, 0.84f, 0.58f));
	}

	private void DrawPreviewCard(Vector2 center, int index)
	{
		Rect2 card = new(center + new Vector2(-92f, -104f), new Vector2(184f, 208f));
		DrawRect(card, new Color(0.085f, 0.09f, 0.11f, 0.96f), true);
		DrawRect(card, new Color(0.3f, 0.34f, 0.39f), false, 1.4f);
		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(12f, 22f), $"Shield #{index + 1}", HorizontalAlignment.Left, card.Size.X - 24f, 13, Colors.White);

		Vector2 unitPos = center + new Vector2(0f, 26f);
		float phase = _time * 2.4f + index * 0.45f;
		bool running = index >= 5;
		bool attacking = index % 3 == 1;
		float stride = running ? Mathf.Sin(phase) * 3.8f : Mathf.Sin(phase * 0.45f) * 0.6f;
		float bob = Mathf.Sin(phase * 0.7f) * 0.8f;
		float attackPose = attacking ? (Mathf.Sin(phase * 1.6f) * 0.5f + 0.5f) : 0f;

		DrawShieldSoldier(unitPos + new Vector2(0f, bob), stride, attackPose, running, index);
	}

	private void DrawShieldSoldier(Vector2 pos, float stride, float attackPose, bool running, int index)
	{
		Color outline = new(0.05f, 0.06f, 0.08f, 0.96f);
		Color body = index < 5
			? new Color(0.52f, 0.94f, 0.72f)
			: new Color(0.84f, 0.84f, 0.88f);
		Color accent = index < 5
			? new Color(0.9f, 0.98f, 1f)
			: new Color(1f, 0.92f, 0.88f);
		float size = 1.42f;
		float runLift = running ? Mathf.Abs(stride) * 0.22f : 0f;
		Vector2 faceSide = index < 5 ? Vector2.Right : Vector2.Left;

		Vector2 feet = pos + new Vector2(0f, 18f + runLift);
		Vector2 hip = feet + new Vector2(0f, -10f * size);
		Vector2 chest = hip + new Vector2(faceSide.X * (running ? stride * 0.18f : attackPose * 1.2f), -12f * size);
		Vector2 head = chest + new Vector2(0f, -9.6f * size);

		Vector2 torsoTopLeft = chest + new Vector2(-7.2f, -3.6f);
		Vector2 torsoTopRight = chest + new Vector2(7.2f, -3.6f);
		Vector2 torsoBottomRight = hip + new Vector2(5.8f, 4.2f);
		Vector2 torsoBottomLeft = hip + new Vector2(-5.8f, 4.2f);
		Vector2[] torso =
		[
			torsoTopLeft,
			torsoTopRight,
			torsoBottomRight,
			torsoBottomLeft,
		];
		DrawColoredPolygon(torso, body);
		DrawPolyline(new[] { torsoTopLeft, torsoTopRight, torsoBottomRight, torsoBottomLeft, torsoTopLeft }, outline, 1.8f);

		DrawCircle(head, 6.1f, outline);
		DrawCircle(head, 4.9f, body.Lerp(Colors.White, 0.1f));

		Vector2 shoulderFront = chest + new Vector2(faceSide.X * 5.3f, -1.8f);
		Vector2 shoulderBack = chest + new Vector2(-faceSide.X * 4.6f, -1.1f);
		Vector2 elbowFront = shoulderFront + new Vector2(faceSide.X * 5.4f, 4.6f - attackPose * 2.2f);
		Vector2 handFront = elbowFront + new Vector2(faceSide.X * 7.4f, 1.6f - attackPose * 2.8f);
		Vector2 elbowBack = shoulderBack + new Vector2(-faceSide.X * 2.2f, 5.2f);
		Vector2 handBack = elbowBack + new Vector2(-faceSide.X * 2.8f, 5.2f);
		DrawLine(shoulderFront, elbowFront, outline, 3.2f);
		DrawLine(elbowFront, handFront, outline, 3f);
		DrawLine(shoulderFront, elbowFront, accent, 1.6f);
		DrawLine(elbowFront, handFront, accent, 1.5f);
		DrawLine(shoulderBack, elbowBack, outline, 2.8f);
		DrawLine(elbowBack, handBack, outline, 2.6f);
		DrawLine(shoulderBack, elbowBack, body.Lerp(Colors.Black, 0.2f), 1.4f);
		DrawLine(elbowBack, handBack, body.Lerp(Colors.Black, 0.24f), 1.3f);

		Vector2 legLeftStart = hip + new Vector2(-3.2f, 2.6f);
		Vector2 legRightStart = hip + new Vector2(3.2f, 2.6f);
		Vector2 kneeLeft = legLeftStart + new Vector2((-1.6f + stride * 0.42f), 9.4f);
		Vector2 kneeRight = legRightStart + new Vector2((1.6f - stride * 0.42f), 9.4f);
		Vector2 footLeft = kneeLeft + new Vector2((-1.4f + stride * 0.3f), 10.4f);
		Vector2 footRight = kneeRight + new Vector2((1.4f - stride * 0.3f), 10.4f);
		DrawLine(legLeftStart, kneeLeft, outline, 3.2f);
		DrawLine(kneeLeft, footLeft, outline, 2.9f);
		DrawLine(legRightStart, kneeRight, outline, 3.2f);
		DrawLine(kneeRight, footRight, outline, 2.9f);
		DrawLine(legLeftStart, kneeLeft, body.Lerp(Colors.Black, 0.16f), 1.5f);
		DrawLine(kneeLeft, footLeft, body.Lerp(Colors.Black, 0.18f), 1.4f);
		DrawLine(legRightStart, kneeRight, body.Lerp(Colors.Black, 0.16f), 1.5f);
		DrawLine(kneeRight, footRight, body.Lerp(Colors.Black, 0.18f), 1.4f);

		DrawShieldWeapon(handFront, faceSide, outline, accent, attackPose);
		DrawShieldBody(handBack, faceSide, outline, accent);
	}

	private void DrawShieldWeapon(Vector2 handFront, Vector2 faceSide, Color outline, Color accent, float attackPose)
	{
		Vector2 weaponBase = handFront + new Vector2(faceSide.X * 1.5f, -0.8f);
		Vector2 weaponTip = weaponBase + new Vector2(faceSide.X * (13f + attackPose * 6f), -5.2f - attackPose * 4.8f);
		DrawLine(weaponBase, weaponTip, outline, 3f);
		DrawLine(weaponBase, weaponTip, accent, 1.5f);
		Vector2 guardA = weaponBase + new Vector2(0f, -3f);
		Vector2 guardB = weaponBase + new Vector2(0f, 3f);
		DrawLine(guardA, guardB, outline, 2.2f);
		DrawLine(guardA, guardB, accent.Lerp(Colors.White, 0.18f), 1.1f);
	}

	private void DrawShieldBody(Vector2 handBack, Vector2 faceSide, Color outline, Color accent)
	{
		Vector2 shieldCenter = handBack + new Vector2(-faceSide.X * 4.2f, 1.4f);
		float shieldH = 11.4f;
		float shieldW = 7.8f;
		Vector2[] shield =
		[
			shieldCenter + new Vector2(0f, -shieldH),
			shieldCenter + new Vector2(shieldW, -shieldH * 0.18f),
			shieldCenter + new Vector2(shieldW * 0.84f, shieldH * 0.76f),
			shieldCenter + new Vector2(0f, shieldH),
			shieldCenter + new Vector2(-shieldW * 0.84f, shieldH * 0.76f),
			shieldCenter + new Vector2(-shieldW, -shieldH * 0.18f),
		];
		DrawColoredPolygon(shield, accent.Lerp(Colors.Black, 0.34f));
		DrawPolyline(new[] { shield[0], shield[1], shield[2], shield[3], shield[4], shield[5], shield[0] }, outline, 1.5f);
		DrawLine(shieldCenter + new Vector2(0f, -shieldH * 0.72f), shieldCenter + new Vector2(0f, shieldH * 0.72f), accent.Lerp(Colors.White, 0.12f), 1f);
	}
}
