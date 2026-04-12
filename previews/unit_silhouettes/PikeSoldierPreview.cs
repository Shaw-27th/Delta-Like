using Godot;

[GlobalClass]
public partial class PikeSoldierPreview : Node2D
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
		DrawString(ThemeDB.FallbackFont, new Vector2(42f, 52f), "Pike Soldier Preview", HorizontalAlignment.Left, -1f, 28, Colors.White);
		DrawString(ThemeDB.FallbackFont, new Vector2(42f, 82f), "Temporary entry. This scene only shows pike soldiers, fixed at 10 samples for silhouette review.", HorizontalAlignment.Left, viewport.X - 84f, 14, new Color(0.82f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, new Vector2(42f, 106f), "Check that the pole starts from the hand, not from the torso center.", HorizontalAlignment.Left, viewport.X - 84f, 13, new Color(0.95f, 0.84f, 0.58f));
	}

	private void DrawPreviewCard(Vector2 center, int index)
	{
		Rect2 card = new(center + new Vector2(-92f, -104f), new Vector2(184f, 208f));
		DrawRect(card, new Color(0.085f, 0.09f, 0.11f, 0.96f), true);
		DrawRect(card, new Color(0.3f, 0.34f, 0.39f), false, 1.4f);
		int poseGroup = index / 3;
		string poseLabel = poseGroup switch
		{
			0 => "Idle",
			1 => "Move",
			_ => "Thrust",
		};
		DrawString(ThemeDB.FallbackFont, card.Position + new Vector2(12f, 22f), $"Pike #{index + 1}  {poseLabel}", HorizontalAlignment.Left, card.Size.X - 24f, 13, Colors.White);

		Vector2 unitPos = center + new Vector2(0f, 28f);
		float phase = _time * 2.1f + index * 0.45f;
		bool running = poseGroup == 1;
		bool thrusting = poseGroup >= 2;
		float stride = running ? Mathf.Sin(phase) * 3.6f : (thrusting ? Mathf.Sin(phase * 0.55f) * 0.8f : Mathf.Sin(phase * 0.35f) * 0.35f);
		float bob = running ? Mathf.Sin(phase * 0.7f) * 0.8f : (thrusting ? Mathf.Sin(phase * 0.5f) * 0.35f : Mathf.Sin(phase * 0.28f) * 0.18f);
		float brace = thrusting ? (Mathf.Sin(phase * 1.5f) * 0.5f + 0.5f) : (running ? 0.42f : 0.18f);

		DrawPikeSoldier(unitPos + new Vector2(0f, bob), stride, brace, running, thrusting, index);
	}

	private void DrawPikeSoldier(Vector2 pos, float stride, float brace, bool running, bool thrusting, int index)
	{
		Color outline = new(0.05f, 0.06f, 0.08f, 0.96f);
		Color body = index < 5
			? new Color(0.52f, 0.94f, 0.72f)
			: new Color(0.84f, 0.84f, 0.88f);
		Color accent = index < 5
			? new Color(0.9f, 0.98f, 1f)
			: new Color(1f, 0.92f, 0.88f);
		Color pike = new(0.62f, 0.48f, 0.28f);

		float size = 1.38f;
		Vector2 faceSide = index < 5 ? Vector2.Right : Vector2.Left;
		float runLift = running ? Mathf.Abs(stride) * 0.2f : 0f;

		Vector2 feet = pos + new Vector2(0f, 18f + runLift);
		Vector2 hip = feet + new Vector2(0f, -10f * size);
		float torsoLean = thrusting ? 2.8f : (running ? 1.1f : 0.2f);
		Vector2 chest = hip + new Vector2(faceSide.X * (stride * 0.14f + torsoLean), -12f * size);
		Vector2 head = chest + new Vector2(0f, -9.4f * size);

		Vector2 torsoTopLeft = chest + new Vector2(-6.6f, -3.4f);
		Vector2 torsoTopRight = chest + new Vector2(6.6f, -3.4f);
		Vector2 torsoBottomRight = hip + new Vector2(5.4f, 4.1f);
		Vector2 torsoBottomLeft = hip + new Vector2(-5.4f, 4.1f);
		Vector2[] torso =
		[
			torsoTopLeft,
			torsoTopRight,
			torsoBottomRight,
			torsoBottomLeft,
		];
		DrawColoredPolygon(torso, body);
		DrawPolyline(new[] { torsoTopLeft, torsoTopRight, torsoBottomRight, torsoBottomLeft, torsoTopLeft }, outline, 1.8f);

		DrawCircle(head, 6f, outline);
		DrawCircle(head, 4.8f, body.Lerp(Colors.White, 0.1f));

		Vector2 shoulderFront = chest + new Vector2(faceSide.X * 4.4f, -1.6f);
		Vector2 shoulderBack = chest + new Vector2(-faceSide.X * 4f, -1.2f);
		Vector2 pikeHand = thrusting
			? shoulderFront + new Vector2(faceSide.X * (11.8f + brace * 2.2f), 0.8f + brace * 0.3f)
			: shoulderFront + new Vector2(faceSide.X * (4.8f + brace * 1.2f), 2.4f + brace * 1.1f);
		Vector2 frontElbow = thrusting
			? shoulderFront.Lerp(pikeHand, 0.48f) + new Vector2(faceSide.X * -0.2f, 1.7f)
			: shoulderFront.Lerp(pikeHand, 0.55f) + new Vector2(faceSide.X * -1.2f, 2.8f);
		Vector2 backElbow = thrusting
			? shoulderBack + new Vector2(faceSide.X * 3.4f, 3.8f)
			: shoulderBack + new Vector2(faceSide.X * 2.1f, 5.1f);
		Vector2 backHand = thrusting
			? shoulderBack.Lerp(pikeHand, 0.54f) + new Vector2(faceSide.X * -0.2f, 3.8f)
			: shoulderBack.Lerp(pikeHand, 0.38f) + new Vector2(faceSide.X * 0.9f, 5.7f);

		DrawLine(shoulderFront, frontElbow, outline, 3f);
		DrawLine(frontElbow, pikeHand, outline, 2.8f);
		DrawLine(shoulderFront, frontElbow, accent, 1.4f);
		DrawLine(frontElbow, pikeHand, accent, 1.3f);
		DrawLine(shoulderBack, backElbow, outline, 3f);
		DrawLine(backElbow, backHand, outline, 2.8f);
		DrawLine(shoulderBack, backElbow, accent, 1.4f);
		DrawLine(backElbow, backHand, accent, 1.3f);

		Vector2 legLeftStart = hip + new Vector2(-3.2f, 2.6f);
		Vector2 legRightStart = hip + new Vector2(3.2f, 2.6f);
		Vector2 kneeLeft = legLeftStart + new Vector2((-1.6f + stride * 0.42f), 9.4f);
		Vector2 kneeRight = legRightStart + new Vector2((1.6f - stride * 0.42f), 9.4f);
		Vector2 footLeft = kneeLeft + new Vector2((-1.4f + stride * 0.3f), 10.4f);
		Vector2 footRight = kneeRight + new Vector2((1.4f - stride * 0.3f), 10.4f);
		DrawLine(legLeftStart, kneeLeft, outline, 3.1f);
		DrawLine(kneeLeft, footLeft, outline, 2.8f);
		DrawLine(legRightStart, kneeRight, outline, 3.1f);
		DrawLine(kneeRight, footRight, outline, 2.8f);
		DrawLine(legLeftStart, kneeLeft, body.Lerp(Colors.Black, 0.16f), 1.45f);
		DrawLine(kneeLeft, footLeft, body.Lerp(Colors.Black, 0.18f), 1.35f);
		DrawLine(legRightStart, kneeRight, body.Lerp(Colors.Black, 0.16f), 1.45f);
		DrawLine(kneeRight, footRight, body.Lerp(Colors.Black, 0.18f), 1.35f);

		DrawPikeFromHand(pikeHand, backHand, faceSide, pike, outline, brace, thrusting);
	}

	private void DrawPikeFromHand(Vector2 frontHand, Vector2 backHand, Vector2 faceSide, Color pike, Color outline, float brace, bool thrusting)
	{
		Vector2 shaftDir = thrusting
			? new Vector2(faceSide.X, -0.04f - brace * 0.04f).Normalized()
			: new Vector2(faceSide.X, -0.24f - brace * 0.12f).Normalized();
		Vector2 shaftRear = thrusting ? backHand - shaftDir * 10f : backHand - shaftDir * 18f;
		Vector2 shaftFront = thrusting ? frontHand + shaftDir * 54f : frontHand + shaftDir * 34f;

		DrawLine(shaftRear, shaftFront, outline, 3.8f);
		DrawLine(shaftRear, shaftFront, pike, 2.2f);

		Vector2 tipBase = shaftFront - shaftDir * 8f;
		Vector2 tipSide = new Vector2(-shaftDir.Y, shaftDir.X) * 3.2f;
		Vector2[] tip =
		[
			shaftFront + shaftDir * 9f,
			tipBase + tipSide,
			tipBase - tipSide,
		];
		DrawColoredPolygon(tip, new Color(0.84f, 0.88f, 0.92f));
		DrawPolyline(new[] { tip[0], tip[1], tip[2], tip[0] }, outline, 1.2f);
	}
}
