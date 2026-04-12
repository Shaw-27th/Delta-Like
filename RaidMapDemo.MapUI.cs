using Godot;

public partial class RaidMapDemo
{
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
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "节点突袭 Demo", HorizontalAlignment.Left, -1f, UiFont(22), Colors.White);
		y += Ui(34f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"回合 {_turn}", HorizontalAlignment.Left, -1f, UiFont(18), new Color(0.76f, 0.84f, 0.95f));
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"生命 {_playerHp}/{_playerMaxHp}   战力 {_playerStrength}", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		y += Ui(26f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"时隙 {_timeSlotProgress}/100   战利品价值 {_lootValue}", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		y += Ui(24f);
		Rect2 timeBar = new(new Vector2(x, y), new Vector2(_sideRect.Size.X - Ui(44f), Ui(12f)));
		DrawRect(timeBar, new Color(0.12f, 0.13f, 0.16f), true);
		DrawRect(timeBar, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawRect(new Rect2(timeBar.Position, new Vector2(timeBar.Size.X * (_timeSlotProgress / 100f), timeBar.Size.Y)), new Color(0.84f, 0.66f, 0.26f), true);
		y += Ui(38f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, _sideRect.Size.X - Ui(44f), UiFont(15), new Color(0.86f, 0.9f, 0.95f));
		y += Ui(64f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"当前节点：{node.Name}", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"类型：{GetNodeTypeLabel(node.Type)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.75f, 0.78f, 0.82f));
		y += Ui(22f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"威胁：{node.Threat}   战利品：{CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.75f, 0.78f, 0.82f));
		y += Ui(22f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"拾取资金：{_runMoneyLooted}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.95f, 0.86f, 0.48f));
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
		DrawButton(autoRect, _autoSearchEnabled ? "自动搜索：开" : "自动搜索：关", _autoSearchEnabled ? new Color(0.24f, 0.56f, 0.32f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(autoRect, "toggle_auto_search"));
		if (node.Type == NodeType.Extract && !_runEnded)
		{
			Rect2 rect = new(new Vector2(x + actionWidth + Ui(16f), y + Ui(42f)), new Vector2(actionWidth, Ui(34f)));
			DrawButton(rect, "执行撤离", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(rect, "extract"));
		}
		y += node.Type == NodeType.Extract ? Ui(88f) : Ui(50f);
		float backpackTop = y + Ui(8f);
		float backpackHeight = DrawTeamBackpackPreview(new Vector2(x, backpackTop));
		float logTop = backpackTop + backpackHeight + Ui(26f);
		logTop = Mathf.Max(logTop, panelBottom - Ui(144f));
		DrawLine(new Vector2(x, logTop - 10f), new Vector2(_sideRect.End.X - 18f, logTop - 10f), new Color(0.24f, 0.27f, 0.31f), 1f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logTop), "世界动态", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
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
}
