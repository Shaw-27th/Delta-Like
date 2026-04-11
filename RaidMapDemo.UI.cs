using Godot;
using System.Collections.Generic;

public partial class RaidMapDemo : Node2D
{
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

	private string GetExitDirectionLabel(Vector2 direction)
	{
		Vector2 dir = direction == Vector2.Zero ? Vector2.Right : direction.Normalized();
		float angle = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(dir.Y, dir.X)) + 360f, 360f);
		string horizontal = dir.X switch
		{
			> 0.38f => "E",
			< -0.38f => "W",
			_ => ""
		};
		string vertical = dir.Y switch
		{
			> 0.38f => "S",
			< -0.38f => "N",
			_ => ""
		};
		string label = $"{vertical}{horizontal}";
		return string.IsNullOrEmpty(label) ? $"{Mathf.RoundToInt(angle)}°" : label;
	}

	private string GetCleanExitDirectionLabel(Vector2 direction) => GetExitDirectionLabel(direction);

	private string GetExitDirectionLabel(int index, int totalCount) => totalCount switch
	{
		1 => index == 0 ? "主出口" : "出口",
		2 => index == 0 ? "西侧" : "东侧",
		3 => index switch
		{
			0 => "西侧",
			1 => "北侧",
			_ => "东侧",
		},
		_ => index switch
		{
			0 => "西侧",
			1 => "北侧",
			2 => "东侧",
			_ => "南侧",
		},
	};


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
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "\u8282\u70b9\u7a81\u88ad Demo", HorizontalAlignment.Left, -1f, UiFont(22), Colors.White);
		y += Ui(34f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u56de\u5408 {_turn}", HorizontalAlignment.Left, -1f, UiFont(18), new Color(0.76f, 0.84f, 0.95f));
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u751f\u547d {_playerHp}/{_playerMaxHp}   \u6218\u529b {_playerStrength}", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		y += Ui(26f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u65f6\u9699 {_timeSlotProgress}/100   \u6218\u5229\u54c1\u4ef7\u503c {_lootValue}", HorizontalAlignment.Left, -1f, UiFont(16), Colors.White);
		y += Ui(24f);
		Rect2 timeBar = new(new Vector2(x, y), new Vector2(_sideRect.Size.X - Ui(44f), Ui(12f)));
		DrawRect(timeBar, new Color(0.12f, 0.13f, 0.16f), true);
		DrawRect(timeBar, new Color(0.34f, 0.37f, 0.42f), false, 1f);
		DrawRect(new Rect2(timeBar.Position, new Vector2(timeBar.Size.X * (_timeSlotProgress / 100f), timeBar.Size.Y)), new Color(0.84f, 0.66f, 0.26f), true);
		y += Ui(38f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _status, HorizontalAlignment.Left, _sideRect.Size.X - Ui(44f), UiFont(15), new Color(0.86f, 0.9f, 0.95f));
		y += Ui(64f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u5f53\u524d\u8282\u70b9\uff1a{node.Name}", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
		y += Ui(28f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u7c7b\u578b\uff1a{GetNodeTypeLabel(node.Type)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.75f, 0.78f, 0.82f));
		y += Ui(22f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u5a01\u80c1\uff1a{node.Threat}   \u6218\u5229\u54c1\uff1a{CountNodeLoot(node)}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.75f, 0.78f, 0.82f));
		y += Ui(22f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"\u62fe\u53d6\u8d44\u91d1\uff1a{_runMoneyLooted}", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.95f, 0.86f, 0.48f));
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
		DrawButton(autoRect, _autoSearchEnabled ? "\u81ea\u52a8\u641c\u7d22\uff1a\u5f00" : "\u81ea\u52a8\u641c\u7d22\uff1a\u5173", _autoSearchEnabled ? new Color(0.24f, 0.56f, 0.32f) : new Color(0.18f, 0.2f, 0.24f));
		_buttons.Add(new ButtonDef(autoRect, "toggle_auto_search"));
		if (node.Type == NodeType.Extract && !_runEnded)
		{
			Rect2 rect = new(new Vector2(x + actionWidth + Ui(16f), y + Ui(42f)), new Vector2(actionWidth, Ui(34f)));
			DrawButton(rect, "\u6267\u884c\u64a4\u79bb", new Color(0.24f, 0.62f, 0.36f));
			_buttons.Add(new ButtonDef(rect, "extract"));
		}
		y += node.Type == NodeType.Extract ? Ui(88f) : Ui(50f);
		float backpackTop = y + Ui(8f);
		float backpackHeight = DrawTeamBackpackPreview(new Vector2(x, backpackTop));
		float logTop = backpackTop + backpackHeight + Ui(26f);
		logTop = Mathf.Max(logTop, panelBottom - Ui(144f));
		DrawLine(new Vector2(x, logTop - 10f), new Vector2(_sideRect.End.X - 18f, logTop - 10f), new Color(0.24f, 0.27f, 0.31f), 1f);
		DrawString(ThemeDB.FallbackFont, new Vector2(x, logTop), "\u4e16\u754c\u52a8\u6001", HorizontalAlignment.Left, -1f, UiFont(18), Colors.White);
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

	private bool HandleHideoutStoragePress(Vector2 click)
	{
		GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize);
		GetSecondaryStorageGridLayout(out Rect2 secondaryGridRect, out Vector2 secondaryGridOrigin, out Vector2 secondaryCellSize);
		bool clickedStash = stashGridRect.HasPoint(click);
		bool clickedSecondary = secondaryGridRect.HasPoint(click);

		if (!clickedStash && !clickedSecondary)
		{
			return false;
		}

		if (_hasDraggedHideoutItem)
		{
			return true;
		}

		if (clickedStash)
		{
			int itemIndex = GetStorageItemIndexAtPoint(click, stashGridOrigin, stashCellSize, _stash);
			if (itemIndex < 0)
			{
				_selectedStashIndex = -1;
				return true;
			}

			_selectedStashIndex = itemIndex;
			_selectedShopIndex = -1;
			_selectedLoadoutIndex = -1;
			_selectedSettlementIndex = -1;
			_pendingHideoutDrag = true;
			_pendingHideoutDragFromLoadout = false;
			_pendingHideoutDragIndex = itemIndex;
			_pendingHideoutGrabOffset = click - GetStorageItemRect(_stash[itemIndex], stashGridOrigin, stashCellSize).Position;
			return true;
		}

		int secondaryIndex = GetStorageItemIndexAtPoint(click, secondaryGridOrigin, secondaryCellSize, _hideoutLoadout);
		if (secondaryIndex < 0)
		{
			if (_inHideout)
			{
				_selectedLoadoutIndex = -1;
			}
			else
			{
				_selectedSettlementIndex = -1;
			}

			return true;
		}

		_selectedStashIndex = -1;
		_selectedShopIndex = -1;
		if (_inHideout)
		{
			_selectedLoadoutIndex = secondaryIndex;
			_selectedSettlementIndex = -1;
		}
		else
		{
			_selectedSettlementIndex = secondaryIndex;
			_selectedLoadoutIndex = -1;
		}

		_pendingHideoutDrag = true;
		_pendingHideoutDragFromLoadout = true;
		_pendingHideoutDragIndex = secondaryIndex;
		_pendingHideoutGrabOffset = click - GetStorageItemRect(_hideoutLoadout[secondaryIndex], secondaryGridOrigin, secondaryCellSize).Position;

		return true;
	}

	private bool HandleHideoutStorageRelease(Vector2 click)
	{
		if (_hasDraggedHideoutItem)
		{
			GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize);
			GetSecondaryStorageGridLayout(out Rect2 secondaryGridRect, out Vector2 secondaryGridOrigin, out Vector2 secondaryCellSize);
			bool clickedStash = stashGridRect.HasPoint(click);
			bool clickedSecondary = secondaryGridRect.HasPoint(click);

			if (clickedStash || clickedSecondary)
			{
				Vector2 cellSize = clickedStash ? stashCellSize : secondaryCellSize;
				Vector2I cell = GetDraggedHideoutTargetCell(clickedStash ? stashGridOrigin : secondaryGridOrigin, cellSize);
				if (clickedStash)
				{
					if (TryPlaceDraggedHideoutItemInStash(cell))
					{
						return true;
					}
				}
				else if (TryPlaceDraggedHideoutItemInSecondary(cell))
				{
					return true;
				}
			}

			RestoreDraggedHideoutItem();
			return true;
		}

		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		return false;
	}

	private void TryBeginPendingHideoutDrag()
	{
		if (!_pendingHideoutDrag || _pendingHideoutDragIndex < 0 || _hasDraggedHideoutItem)
		{
			return;
		}

		if (_pendingHideoutDragFromLoadout)
		{
			if (_pendingHideoutDragIndex >= _hideoutLoadout.Count)
			{
				_pendingHideoutDrag = false;
				_pendingHideoutDragIndex = -1;
				return;
			}

			_draggedHideoutItem = _hideoutLoadout[_pendingHideoutDragIndex];
			_draggedHideoutOriginalCell = _draggedHideoutItem.Cell;
			_draggedHideoutGrabOffset = _pendingHideoutGrabOffset;
			_draggedHideoutFromLoadout = true;
			_hasDraggedHideoutItem = true;
			_hideoutLoadout.RemoveAt(_pendingHideoutDragIndex);
			_selectedLoadoutIndex = -1;
			_selectedSettlementIndex = -1;
		}
		else
		{
			if (_pendingHideoutDragIndex >= _stash.Count)
			{
				_pendingHideoutDrag = false;
				_pendingHideoutDragIndex = -1;
				return;
			}

			_draggedHideoutItem = _stash[_pendingHideoutDragIndex];
			_draggedHideoutOriginalCell = _draggedHideoutItem.Cell;
			_draggedHideoutGrabOffset = _pendingHideoutGrabOffset;
			_draggedHideoutFromLoadout = false;
			_hasDraggedHideoutItem = true;
			_stash.RemoveAt(_pendingHideoutDragIndex);
			_selectedStashIndex = -1;
		}

		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		_pendingHideoutGrabOffset = Vector2.Zero;
	}

	private void RestoreDraggedHideoutItem()
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return;
		}

		_draggedHideoutItem.Cell = _draggedHideoutOriginalCell;
		if (_draggedHideoutFromLoadout)
		{
			_hideoutLoadout.Add(_draggedHideoutItem);
		}
		else
		{
			_stash.Add(_draggedHideoutItem);
		}

		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		_pendingHideoutGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
	}

	private Rect2 GetStorageItemRect(BackpackItem item, Vector2 gridOrigin, Vector2 cellSize)
	{
		return new Rect2(
			gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
			new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
	}

	private Vector2I GetDraggedHideoutTargetCell(Vector2 gridOrigin, Vector2 cellSize)
	{
		Vector2 dragOrigin = GetViewport().GetMousePosition() - _draggedHideoutGrabOffset;
		Vector2 local = dragOrigin - gridOrigin + new Vector2(Ui(1f), Ui(1f));
		return new Vector2I(
			Mathf.FloorToInt(local.X / cellSize.X),
			Mathf.FloorToInt(local.Y / cellSize.Y));
	}

	private bool TryPlaceDraggedHideoutItemInStash(Vector2I cell)
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return false;
		}

		if (cell.X < 0 || cell.Y < 0
			|| cell.X + _draggedHideoutItem.Size.X > StashGridWidth
			|| cell.Y + _draggedHideoutItem.Size.Y > StashGridHeight
			|| !IsStorageAreaFree(cell, _draggedHideoutItem.Size, _stash))
		{
			return false;
		}

		_draggedHideoutItem.Cell = cell;
		_stash.Add(_draggedHideoutItem);
		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private bool TryPlaceDraggedHideoutItemInSecondary(Vector2I cell)
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return false;
		}

		List<BackpackCapacityBlock> blocks = BuildHideoutLoadoutCapacityBlocks();
		if (!IsAreaEnabledInBlocks(cell, _draggedHideoutItem.Size, blocks) || !IsStorageAreaFree(cell, _draggedHideoutItem.Size, _hideoutLoadout))
		{
			return false;
		}

		_draggedHideoutItem.Cell = cell;
		_hideoutLoadout.Add(_draggedHideoutItem);
		_hasDraggedHideoutItem = false;
		_draggedHideoutItem = null;
		_draggedHideoutGrabOffset = Vector2.Zero;
		_pendingHideoutDrag = false;
		_pendingHideoutDragIndex = -1;
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private void GetHideoutLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 shopRect, out Rect2 loadoutRect)
	{
		Vector2 viewport = GetViewportRect().Size;
		Vector2 panelSize = new(
			Mathf.Clamp(viewport.X - Ui(120f), 1100f, 1480f),
			Mathf.Clamp(viewport.Y - Ui(90f), 660f, 900f));
		panel = new Rect2((viewport - panelSize) * 0.5f, panelSize);

		float contentTop = panel.Position.Y + Ui(272f);
		float contentBottom = panel.End.Y - Ui(18f);
		float contentHeight = Mathf.Max(Ui(260f), contentBottom - contentTop);
		float gap = Ui(20f);
		float stashWidth = Mathf.Max(Ui(320f), panel.Size.X * 0.46f);
		float rightWidth = panel.Size.X - Ui(36f) - stashWidth - gap;
		float rightX = panel.Position.X + Ui(18f) + stashWidth + gap;
		float stackGap = Ui(16f);
		float topHeight = Mathf.Max(Ui(220f), contentHeight * 0.5f - stackGap * 0.5f);
		float bottomHeight = contentHeight - topHeight - stackGap;

		stashRect = new Rect2(new Vector2(panel.Position.X + Ui(18f), contentTop), new Vector2(stashWidth, contentHeight));
		shopRect = new Rect2(new Vector2(rightX, contentTop), new Vector2(rightWidth, topHeight));
		loadoutRect = new Rect2(new Vector2(rightX, shopRect.End.Y + stackGap), new Vector2(rightWidth, bottomHeight));
	}

	private void GetSettlementLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 backpackRect)
	{
		Vector2 viewport = GetViewportRect().Size;
		Vector2 panelSize = new(
			Mathf.Clamp(viewport.X - Ui(120f), 1080f, 1440f),
			Mathf.Clamp(viewport.Y - Ui(100f), 640f, 860f));
		panel = new Rect2((viewport - panelSize) * 0.5f, panelSize);

		float contentTop = panel.Position.Y + Ui(86f);
		float contentBottom = panel.End.Y - Ui(22f);
		float contentHeight = contentBottom - contentTop;
		float gap = Ui(18f);
		float leftWidth = Mathf.Max(Ui(320f), panel.Size.X * 0.46f);
		float rightWidth = panel.Size.X - Ui(36f) - leftWidth - gap;
		stashRect = new Rect2(new Vector2(panel.Position.X + Ui(18f), contentTop), new Vector2(leftWidth, contentHeight));
		backpackRect = new Rect2(new Vector2(stashRect.End.X + gap, contentTop), new Vector2(rightWidth, contentHeight));
	}

	private void GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize)
	{
		if (_runEnded && _showSettlementTransfer)
		{
			GetSettlementLayout(out _, out Rect2 stashRect, out _);
			stashCellSize = new Vector2(Ui(22f), Ui(22f));
			stashGridOrigin = stashRect.Position + new Vector2(Ui(14f), Ui(46f));
			stashGridRect = new Rect2(stashGridOrigin, new Vector2(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y));
			return;
		}

		GetHideoutLayout(out _, out Rect2 hideoutStashRect, out _, out _);
		stashCellSize = new Vector2(Ui(22f), Ui(22f));
		stashGridOrigin = hideoutStashRect.Position + new Vector2(Ui(14f), Ui(46f));
		stashGridRect = new Rect2(stashGridOrigin, new Vector2(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y));
	}

	private void GetSecondaryStorageGridLayout(out Rect2 gridRect, out Vector2 gridOrigin, out Vector2 cellSize)
	{
		cellSize = new Vector2(Ui(18f), Ui(18f));
		if (_runEnded && _showSettlementTransfer)
		{
			GetSettlementLayout(out _, out _, out Rect2 backpackRect);
			gridOrigin = backpackRect.Position + new Vector2(Ui(14f), Ui(42f));
		}
		else
		{
			GetHideoutLayout(out _, out _, out _, out Rect2 loadoutRect);
			gridOrigin = loadoutRect.Position + new Vector2(Ui(14f), Ui(42f));
		}

		gridRect = new Rect2(gridOrigin, new Vector2(TeamBackpackMaxWidth * cellSize.X, TeamBackpackMaxRows * cellSize.Y));
	}
	private int GetStorageItemIndexAtPoint(Vector2 point, Vector2 gridOrigin, Vector2 cellSize, List<BackpackItem> items)
	{
		for (int i = items.Count - 1; i >= 0; i--)
		{
			BackpackItem item = items[i];
			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
			if (itemRect.HasPoint(point))
			{
				return i;
			}
		}

		return -1;
	}

	private void GetBackpackPreviewLayout(out Vector2 gridOrigin, out Vector2 cellSize, out int gridWidth, out int gridHeight)
	{
		float x = _sideRect.Position.X + Ui(22f);
		float y = _sideRect.Position.Y + Ui(34f);
		y += Ui(34f);
		y += Ui(28f);
		y += Ui(26f);
		y += Ui(24f);
		y += Ui(38f);
		y += Ui(64f);
		y += Ui(28f);
		y += Ui(22f);
		y += Ui(22f);
		y += Ui(22f);
		y += Ui(20f);
		if (IsOverloaded())
		{
			y += Ui(20f);
		}

		y += _nodes[_playerNodeId].Type == NodeType.Extract ? Ui(88f) : Ui(50f);
		Vector2 origin = new(x, y + Ui(8f));
		cellSize = new Vector2(Ui(18f), Ui(18f));
		gridOrigin = origin + new Vector2(0f, Ui(30f));
		gridWidth = GetBackpackPreviewGridWidth();
		gridHeight = GetBackpackPreviewGridHeight();
	}

	private float DrawTeamBackpackPreview(Vector2 origin)
	{
		Vector2 cellSize = new(Ui(18f), Ui(18f));
		DrawString(ThemeDB.FallbackFont, origin, "队伍背包", HorizontalAlignment.Left, -1f, UiFont(14), new Color(0.9f, 0.92f, 0.96f));
		Rect2 autoPackRect = new(origin + new Vector2(Ui(114f), Ui(-6f)), new Vector2(Ui(92f), Ui(28f)));
		DrawButton(autoPackRect, "自动整理", new Color(0.28f, 0.42f, 0.26f));
		_buttons.Add(new ButtonDef(autoPackRect, "auto_pack"));
		if (_hasDraggedBackpackItem && !_inHideout && !_runEnded)
		{
			Rect2 dropDraggedRect = new(origin + new Vector2(Ui(214f), Ui(-6f)), new Vector2(Ui(92f), Ui(28f)));
			DrawButton(dropDraggedRect, "丢到地面", new Color(0.46f, 0.28f, 0.22f));
			_buttons.Add(new ButtonDef(dropDraggedRect, "drop_dragged_backpack"));
		}
		Vector2 gridOrigin = origin + new Vector2(0f, Ui(30f));
		List<BackpackCapacityBlock> blocks = BuildCurrentBackpackCapacityBlocks();
		int gridWidth = GetBackpackPreviewGridWidth();
		int gridHeight = GetBackpackPreviewGridHeight();
		for (int i = 0; i < blocks.Count; i++)
		{
			BackpackCapacityBlock block = blocks[i];
			Color blockTint = i % 2 == 0 ? new Color(0.16f, 0.19f, 0.24f, 0.55f) : new Color(0.19f, 0.17f, 0.23f, 0.55f);
			Rect2 blockRect = new(
				gridOrigin + new Vector2(block.Cell.X * cellSize.X, block.Cell.Y * cellSize.Y),
				new Vector2(block.Size.X * cellSize.X - Ui(2f), block.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(blockRect, blockTint, true);
			for (int y = 0; y < block.Size.Y; y++)
			{
				for (int x = 0; x < block.Size.X; x++)
				{
					Rect2 cellRect = new(
						gridOrigin + new Vector2((block.Cell.X + x) * cellSize.X, (block.Cell.Y + y) * cellSize.Y),
						cellSize - new Vector2(Ui(2f), Ui(2f)));
					DrawRect(cellRect, new Color(0.08f, 0.09f, 0.11f), true);
					DrawRect(cellRect, new Color(0.2f, 0.23f, 0.28f), false, 1f);
				}
			}
			DrawDashedRect(blockRect, new Color(0.82f, 0.86f, 0.94f, 0.45f));
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		string hoveredItemLabel = "";
		foreach (BackpackItem item in _runBackpack)
		{
			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, new Color(1f, 1f, 1f, 0.82f), false, 1f);
			if (itemRect.Size.X >= Ui(40f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}

			if (itemRect.HasPoint(mouse))
			{
				hoveredItemLabel = item.Label;
			}
		}

		float overflowWidth = Ui(132f);
		Vector2 overflowOrigin = gridOrigin + new Vector2(gridWidth * cellSize.X + Ui(18f), 0f);
		int overflowVisible = Mathf.Max(1, _overflowBackpackItems.Count);
		float overflowButtonHeight = _overflowBackpackItems.Count > 0 ? Ui(28f) : 0f;
		Rect2 overflowRect = new(
			overflowOrigin,
			new Vector2(overflowWidth, Mathf.Max(Ui(72f), overflowVisible * Ui(28f) + Ui(18f) + overflowButtonHeight + (_overflowBackpackItems.Count > 0 ? Ui(8f) : 0f))));
		DrawRect(overflowRect, new Color(0.07f, 0.06f, 0.05f, 0.92f), true);
		DrawRect(overflowRect, _overflowBackpackItems.Count > 0 ? new Color(0.8f, 0.44f, 0.4f, 0.95f) : new Color(0.3f, 0.32f, 0.36f, 0.85f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, overflowRect.Position + new Vector2(Ui(8f), Ui(16f)), "待整理", HorizontalAlignment.Left, overflowRect.Size.X - Ui(12f), UiFont(12), Colors.White);
		if (_overflowBackpackItems.Count == 0)
		{
			DrawString(ThemeDB.FallbackFont, overflowRect.Position + new Vector2(Ui(8f), Ui(38f)), "无物品", HorizontalAlignment.Left, overflowRect.Size.X - Ui(12f), UiFont(11), new Color(0.72f, 0.76f, 0.82f));
		}
		else
		{
			for (int i = 0; i < _overflowBackpackItems.Count; i++)
			{
				BackpackItem item = _overflowBackpackItems[i];
				Rect2 overflowItemRect = new(
					overflowRect.Position + new Vector2(Ui(8f), Ui(24f) + i * Ui(28f)),
					new Vector2(overflowRect.Size.X - Ui(16f), Ui(22f)));
				Color itemColor = GetGridRarityColor(item.Rarity);
				DrawRect(overflowItemRect, new Color(itemColor.R, itemColor.G, itemColor.B, 0.92f), true);
				DrawRect(overflowItemRect, new Color(1f, 1f, 1f, 0.75f), false, 1f);
				DrawString(ThemeDB.FallbackFont, overflowItemRect.Position + new Vector2(Ui(4f), Ui(14f)), $"{item.Label} {item.Size.X}x{item.Size.Y}", HorizontalAlignment.Left, overflowItemRect.Size.X - Ui(8f), UiFont(10), Colors.Black);
				if (overflowItemRect.HasPoint(mouse))
				{
					hoveredItemLabel = item.Label;
				}
			}

			Rect2 dropRect = new(
				new Vector2(overflowRect.Position.X + Ui(8f), overflowRect.End.Y - Ui(32f)),
				new Vector2(overflowRect.Size.X - Ui(16f), Ui(24f)));
			DrawButton(dropRect, "丢到地面", new Color(0.46f, 0.28f, 0.22f));
			_buttons.Add(new ButtonDef(dropRect, "drop_overflow"));
		}

		if (!string.IsNullOrEmpty(hoveredItemLabel))
		{
			DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f)), hoveredItemLabel);
		}

		return Ui(36f) + Mathf.Max(gridHeight * cellSize.Y, overflowRect.Size.Y);
	}

	private void DrawDraggedBackpackOverlay()
	{
		if (!_hasDraggedBackpackItem || _draggedBackpackItem == null || _inHideout || _runEnded)
		{
			return;
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		Vector2 cellSize = new(Ui(18f), Ui(18f));
		Vector2 dragSize = new(_draggedBackpackItem.Size.X * cellSize.X - Ui(2f), _draggedBackpackItem.Size.Y * cellSize.Y - Ui(2f));
		Vector2 drawPos = mouse - _draggedBackpackGrabOffset;
		Rect2 dragRect = new(drawPos, dragSize);
		Color dragColor = GetGridRarityColor(_draggedBackpackItem.Rarity);

		GetBackpackPreviewLayout(out Vector2 gridOrigin, out Vector2 previewCellSize, out int _, out int _);
		Vector2I previewCell = GetDraggedBackpackTargetCell(gridOrigin, previewCellSize);
		if (previewCell.X >= 0 && previewCell.Y >= 0
			&& previewCell.X + _draggedBackpackItem.Size.X <= TeamBackpackMaxWidth
			&& previewCell.Y + _draggedBackpackItem.Size.Y <= TeamBackpackMaxRows)
		{
			Rect2 targetRect = GetBackpackItemRect(new BackpackItem { Cell = previewCell, Size = _draggedBackpackItem.Size }, gridOrigin, previewCellSize);
			bool canPlace = IsBackpackAreaEnabled(previewCell, _draggedBackpackItem.Size) && IsBackpackAreaFree(previewCell, _draggedBackpackItem.Size);
			DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
			DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
		}

		DrawRect(dragRect, new Color(dragColor.R, dragColor.G, dragColor.B, 0.84f), true);
		DrawRect(dragRect, new Color(1f, 1f, 1f, 0.9f), false, 1f);
		DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f) + dragRect.Size.Y + Ui(4f)), _draggedBackpackItem.Label);
	}

	private void DrawDraggedHideoutOverlay()
	{
		if (!_hasDraggedHideoutItem || _draggedHideoutItem == null)
		{
			return;
		}

		Vector2 mouse = GetViewport().GetMousePosition();
		Vector2 cellSize = _draggedHideoutFromLoadout ? new Vector2(Ui(18f), Ui(18f)) : new Vector2(Ui(22f), Ui(22f));
		Vector2 dragSize = new(_draggedHideoutItem.Size.X * cellSize.X - Ui(2f), _draggedHideoutItem.Size.Y * cellSize.Y - Ui(2f));
		Vector2 drawPos = mouse - _draggedHideoutGrabOffset;
		Rect2 dragRect = new(drawPos, dragSize);
		Color dragColor = GetGridRarityColor(_draggedHideoutItem.Rarity);

		GetHideoutStashGridLayout(out Rect2 stashGridRect, out Vector2 stashGridOrigin, out Vector2 stashCellSize);
		GetSecondaryStorageGridLayout(out Rect2 secondaryGridRect, out Vector2 secondaryGridOrigin, out Vector2 secondaryCellSize);
		if (stashGridRect.HasPoint(mouse))
		{
			Vector2I cell = GetDraggedHideoutTargetCell(stashGridOrigin, stashCellSize);
			if (cell.X >= 0 && cell.Y >= 0 && cell.X + _draggedHideoutItem.Size.X <= StashGridWidth && cell.Y + _draggedHideoutItem.Size.Y <= StashGridHeight)
			{
				Rect2 targetRect = GetStorageItemRect(new BackpackItem { Cell = cell, Size = _draggedHideoutItem.Size }, stashGridOrigin, stashCellSize);
				bool canPlace = IsStorageAreaFree(cell, _draggedHideoutItem.Size, _stash);
				DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
				DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
			}
		}
		else if (secondaryGridRect.HasPoint(mouse))
		{
			Vector2I cell = GetDraggedHideoutTargetCell(secondaryGridOrigin, secondaryCellSize);
			Rect2 targetRect = GetStorageItemRect(new BackpackItem { Cell = cell, Size = _draggedHideoutItem.Size }, secondaryGridOrigin, secondaryCellSize);
			bool canPlace = IsAreaEnabledInBlocks(cell, _draggedHideoutItem.Size, BuildHideoutLoadoutCapacityBlocks()) && IsStorageAreaFree(cell, _draggedHideoutItem.Size, _hideoutLoadout);
			DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
			DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
		}

		DrawRect(dragRect, new Color(dragColor.R, dragColor.G, dragColor.B, 0.84f), true);
		DrawRect(dragRect, new Color(1f, 1f, 1f, 0.9f), false, 1f);
		DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f) + dragRect.Size.Y + Ui(4f)), _draggedHideoutItem.Label);
	}

	private int CountAvailableEquipped(LootContainer container)
	{
		int count = 0;
		foreach (EquippedLoot equipped in container.EquippedItems)
		{
			if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
			{
				count++;
			}
		}
		return count;
	}

	private int CountRevealedGridItems(LootContainer container)
	{
		if (container.GridItems.Count == 0)
		{
			return container.VisibleItems.Count;
		}

		int count = 0;
		foreach (GridLootItem item in container.GridItems)
		{
			if (item.Revealed && !item.Taken)
			{
				count++;
			}
		}
		return count;
	}

	private int CountHiddenGridItems(LootContainer container)
	{
		if (container.GridItems.Count == 0)
		{
			return container.HiddenRemaining;
		}

		int count = 0;
		foreach (GridLootItem item in container.GridItems)
		{
			if (!item.Revealed && !item.Taken)
			{
				count++;
			}
		}
		return count;
	}

	private int CountTakeableContainerItems(LootContainer container)
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
				if (item.Revealed && !item.Taken)
				{
					count++;
				}
			}

			return count;
		}

		return count + container.VisibleItems.Count;
	}

	private void DrawContainerPopup()
	{
		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex < 0 || _selectedContainerIndex >= node.Containers.Count)
		{
			return;
		}
		LootContainer container = node.Containers[_selectedContainerIndex];
		EnsureContainerGrid(container);
		GetContainerPopupGridLayout(container, out Rect2 panel, out Vector2 gridOrigin, out Vector2 cellSize);
		DrawRect(panel, new Color(0.08f, 0.09f, 0.11f, 0.7f), true);
		DrawRect(panel, new Color(1f, 1f, 1f, 0.82f), false, 2f);
		Rect2 titleBar = new(panel.Position + new Vector2(1f, 1f), new Vector2(panel.Size.X - 2f, 30f));
		DrawRect(titleBar, new Color(0.12f, 0.13f, 0.16f, 0.72f), true);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14f, 21f), container.Label, HorizontalAlignment.Left, 190f, 14, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(panel.Size.X - 102f, 21f), GetContainerKindLabel(container.Kind), HorizontalAlignment.Left, 68f, 12, new Color(0.93f, 0.84f, 0.58f));
		int takeableCount = CountTakeableContainerItems(container);
		if (takeableCount > 0)
		{
			Rect2 takeAllRect = new(new Vector2(panel.End.X - 92f, panel.Position.Y + 4f), new Vector2(78f, 22f));
			DrawButton(takeAllRect, "拾取全部", new Color(0.28f, 0.46f, 0.62f));
			_buttons.Add(new ButtonDef(takeAllRect, "take_all", _selectedContainerIndex));
		}
		if (_hasDraggedBackpackItem && _draggedBackpackItem != null)
		{
			DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(14f, 38f), "背包物品已拿起：点击下方容器网格即可放回。", HorizontalAlignment.Left, panel.Size.X - 28f, 11, new Color(0.92f, 0.94f, 0.78f));
		}
		float rowY = panel.Position.Y + (_hasDraggedBackpackItem && _draggedBackpackItem != null ? 56f : 42f);
		if (container.Kind == ContainerKind.EliteCorpse)
		{
			DrawString(ThemeDB.FallbackFont, new Vector2(panel.Position.X + 14f, rowY), "装备栏", HorizontalAlignment.Left, -1f, 13, new Color(0.9f, 0.92f, 0.96f));
			rowY += 16f;
			for (int equipIndex = 0; equipIndex < container.EquippedItems.Count; equipIndex++)
			{
				EquippedLoot equipped = container.EquippedItems[equipIndex];
				Rect2 slot = new(new Vector2(panel.Position.X + 14f, rowY), new Vector2(panel.Size.X - 28f, 28f));
				DrawRect(slot, new Color(0.14f, 0.16f, 0.2f), true);
				DrawRect(slot, new Color(0.5f, 0.66f, 0.86f), false, 1f);
				string label = string.IsNullOrEmpty(equipped.Label) || equipped.Taken ? $"{GetEquipmentSlotLabel(equipped.Slot)}\uff1a\u7a7a" : $"{GetEquipmentSlotLabel(equipped.Slot)}\uff1a{equipped.Label}";
				DrawString(ThemeDB.FallbackFont, slot.Position + new Vector2(10f, 19f), label, HorizontalAlignment.Left, slot.Size.X - 86f, 12, Colors.White);
				if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
				{
					Rect2 takeRect = new(new Vector2(slot.End.X - 70f, slot.Position.Y + 2f), new Vector2(58f, 24f));
					DrawButton(takeRect, "\u62ff\u53d6", new Color(0.26f, 0.42f, 0.58f));
					_buttons.Add(new ButtonDef(takeRect, "take", _selectedContainerIndex * 100 + 50 + equipIndex));
				}
				rowY += 34f;
			}
			rowY += 8f;
		}

		DrawString(ThemeDB.FallbackFont, new Vector2(panel.Position.X + 14f, rowY), "背包", HorizontalAlignment.Left, -1f, 13, new Color(0.9f, 0.92f, 0.96f));
		rowY += 16f;
		for (int y = 0; y < container.GridSize.Y; y++)
		{
			for (int x = 0; x < container.GridSize.X; x++)
			{
				Rect2 cellRect = new(gridOrigin + new Vector2(x * cellSize.X, y * cellSize.Y), cellSize - new Vector2(2f, 2f));
				DrawRect(cellRect, new Color(0.09f, 0.1f, 0.12f), true);
				DrawRect(cellRect, new Color(0.24f, 0.26f, 0.3f), false, 1f);
			}
		}

		if (_hasDraggedBackpackItem && _draggedBackpackItem != null)
		{
			Vector2I targetCell = GetDraggedBackpackTargetCell(gridOrigin, cellSize);
			if (targetCell.X >= 0 && targetCell.Y >= 0
				&& targetCell.X + _draggedBackpackItem.Size.X <= container.GridSize.X
				&& targetCell.Y + _draggedBackpackItem.Size.Y <= container.GridSize.Y)
			{
				Rect2 targetRect = new(
					gridOrigin + new Vector2(targetCell.X * cellSize.X, targetCell.Y * cellSize.Y),
					new Vector2(_draggedBackpackItem.Size.X * cellSize.X - 2f, _draggedBackpackItem.Size.Y * cellSize.Y - 2f));
				bool canPlace = IsGridAreaFree(container, targetCell, _draggedBackpackItem.Size);
				DrawRect(targetRect, new Color(1f, 1f, 1f, 0.08f), true);
				DrawRect(targetRect, canPlace ? new Color(0.42f, 0.96f, 0.58f, 0.95f) : new Color(0.96f, 0.34f, 0.34f, 0.95f), false, 2f);
			}
		}

		for (int itemIndex = 0; itemIndex < container.GridItems.Count; itemIndex++)
		{
			GridLootItem item = container.GridItems[itemIndex];
			if (item.Taken)
			{
				continue;
			}

			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - 2f, item.Size.Y * cellSize.Y - 2f));

			if (!item.Revealed)
			{
				DrawRect(itemRect, new Color(0.3f, 0.32f, 0.35f), true);
				DrawRect(itemRect, new Color(0.62f, 0.64f, 0.67f), false, 1.2f);
				DrawHiddenItemPattern(itemRect, new Color(0.55f, 0.57f, 0.6f, 0.65f));
				if (container.ActiveSearchItemIndex < 0 || container.ActiveSearchItemIndex == itemIndex)
				{
					_buttons.Add(new ButtonDef(itemRect, "search", _selectedContainerIndex * 100 + itemIndex));
				}
			}
			else
			{
				DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
				DrawRect(itemRect, Colors.White, false, 1.2f);
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(4f, 16f), item.Label, HorizontalAlignment.Left, itemRect.Size.X - 6f, 10, Colors.Black);
				_buttons.Add(new ButtonDef(itemRect, "take", _selectedContainerIndex * 100 + itemIndex));
			}

			if (container.ActiveSearchItemIndex == itemIndex && !item.Revealed)
			{
				Vector2 center = itemRect.GetCenter();
				float ratio = Mathf.Clamp(item.SearchProgress / item.SearchTime, 0f, 0.98f);
				float endAngle = -Mathf.Pi / 2f + Mathf.Tau * ratio;
				DrawArc(center, 10f, -Mathf.Pi / 2f, endAngle, 24, GetGridRarityColor(item.Rarity), 3f);
			}
		}
	}

	private void DrawHiddenItemPattern(Rect2 rect, Color color)
	{
		float left = rect.Position.X;
		float right = rect.End.X;
		float top = rect.Position.Y;
		float bottom = rect.End.Y;
		for (float offset = -rect.Size.Y; offset < rect.Size.X; offset += 8f)
		{
			float c = left + offset + bottom;
			List<Vector2> intersections = new(4);

			float xOnBottom = c - bottom;
			if (xOnBottom >= left && xOnBottom <= right) intersections.Add(new Vector2(xOnBottom, bottom));
			float xOnTop = c - top;
			if (xOnTop >= left && xOnTop <= right) intersections.Add(new Vector2(xOnTop, top));
			float yOnLeft = c - left;
			if (yOnLeft >= top && yOnLeft <= bottom) AddUniquePoint(intersections, new Vector2(left, yOnLeft));
			float yOnRight = c - right;
			if (yOnRight >= top && yOnRight <= bottom) AddUniquePoint(intersections, new Vector2(right, yOnRight));

			if (intersections.Count >= 2)
			{
				DrawLine(intersections[0], intersections[1], color, 1f);
			}
		}
	}

	private void GetContainerPopupGridLayout(LootContainer container, out Rect2 panel, out Vector2 gridOrigin, out Vector2 cellSize)
	{
		EnsureContainerGrid(container);
		cellSize = new Vector2(26f, 26f);
		float equipmentHeight = container.Kind == ContainerKind.EliteCorpse ? 118f : 0f;
		float gridWidth = container.GridSize.X * cellSize.X + 26f;
		Vector2 panelSize = new(Mathf.Max(gridWidth + 28f, 356f), container.GridSize.Y * cellSize.Y + 96f + equipmentHeight);
		panel = new Rect2((GetViewportRect().Size - panelSize) / 2f, panelSize);
		float rowY = panel.Position.Y + (_hasDraggedBackpackItem && _draggedBackpackItem != null ? 56f : 42f);
		if (container.Kind == ContainerKind.EliteCorpse)
		{
			rowY += 16f;
			rowY += container.EquippedItems.Count * 34f;
			rowY += 8f;
		}

		rowY += 16f;
		gridOrigin = new Vector2(panel.Position.X + (panel.Size.X - gridWidth) * 0.5f, rowY);
	}

	private static void AddUniquePoint(List<Vector2> points, Vector2 point)
	{
		foreach (Vector2 existing in points)
		{
			if (existing.DistanceSquaredTo(point) < 0.25f)
			{
				return;
			}
		}

		points.Add(point);
	}

	private static Color GetGridRarityColor(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => new Color(0.82f, 0.84f, 0.87f),
		ItemRarity.Green => new Color(0.48f, 0.85f, 0.45f),
		ItemRarity.Blue => new Color(0.36f, 0.7f, 1f),
		ItemRarity.Purple => new Color(0.72f, 0.48f, 0.92f),
		_ => new Color(1f, 0.82f, 0.32f),
	};

	private void DrawEncounterPrompt()
	{
		if (_pendingEncounter == null)
		{
			return;
		}

		Rect2 panel = new(new Vector2(360f, 250f), new Vector2(420f, 170f));
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.45f), true);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.98f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 30f), "遭遇敌人", HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 66f), _pendingEncounter.PromptText, HorizontalAlignment.Left, 380f, 14, new Color(0.84f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 96f), $"敌方：{_pendingEncounter.EnemyName}", HorizontalAlignment.Left, 380f, 13, new Color(0.95f, 0.78f, 0.78f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(20f, 118f), "当前仅可选择战斗。", HorizontalAlignment.Left, 380f, 13, new Color(0.86f, 0.9f, 0.95f));
		Rect2 fightRect = new(new Vector2(panel.Position.X + 20f, panel.End.Y - 42f), new Vector2(92f, 28f));
		DrawButton(fightRect, "战斗", new Color(0.54f, 0.26f, 0.22f));
		_buttons.Add(new ButtonDef(fightRect, "encounter_fight"));
	}

	private void DrawSearchConfirmDialog()
	{
		Rect2 panel = new(new Vector2(360f, 240f), new Vector2(480f, 210f));
		DrawRect(new Rect2(Vector2.Zero, GetViewportRect().Size), new Color(0f, 0f, 0f, 0.45f), true);
		DrawRect(panel, new Color(0.05f, 0.05f, 0.06f, 0.98f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 32f), "\u5151\u6362\u641c\u7d22\u6b21\u6570", HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 68f), "\u5f53\u524d\u641c\u7d22\u6b21\u6570\u4e0d\u8db3\u3002\u662f\u5426\u82b1\u8d39 1 \u56de\u5408\u6362\u53d6 4 \u6b21\u641c\u7d22\uff0c", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 90f), "\u5e76\u7acb\u5373\u63ed\u793a\u8fd9\u4e2a\u5bb9\u5668\u7684\u4e0b\u4e00\u4ef6\u7269\u54c1\uff1f", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		Rect2 checkRect = new(new Vector2(panel.Position.X + 24f, panel.Position.Y + 126f), new Vector2(18f, 18f));
		DrawRect(checkRect, new Color(0.08f, 0.08f, 0.1f), true);
		DrawRect(checkRect, Colors.White, false, 1.2f);
		if (_confirmSkipChecked)
		{
			DrawLine(checkRect.Position + new Vector2(3f, 9f), checkRect.Position + new Vector2(7f, 14f), new Color(0.56f, 0.95f, 0.62f), 2f);
			DrawLine(checkRect.Position + new Vector2(7f, 14f), checkRect.Position + new Vector2(15f, 3f), new Color(0.56f, 0.95f, 0.62f), 2f);
		}
		_buttons.Add(new ButtonDef(checkRect, "toggle_confirm_skip"));
		DrawString(ThemeDB.FallbackFont, checkRect.Position + new Vector2(28f, 14f), "\u672c\u6b21\u63a2\u7d22\u540e\u7eed\u4e0d\u518d\u63d0\u793a", HorizontalAlignment.Left, -1f, 13, Colors.White);
		Rect2 yesRect = new(new Vector2(panel.Position.X + 24f, panel.End.Y - 46f), new Vector2(90f, 28f));
		Rect2 noRect = new(new Vector2(panel.Position.X + 126f, panel.End.Y - 46f), new Vector2(90f, 28f));
		DrawButton(yesRect, "\u786e\u8ba4", new Color(0.24f, 0.56f, 0.34f));
		DrawButton(noRect, "\u53d6\u6d88", new Color(0.38f, 0.22f, 0.22f));
		_buttons.Add(new ButtonDef(yesRect, "confirm_search_yes"));
		_buttons.Add(new ButtonDef(noRect, "confirm_search_no"));
	}
	private void DrawEndOverlay()
	{
		Vector2 viewport = GetViewportRect().Size;
		Rect2 panel = new(viewport * 0.5f - new Vector2(360f, 210f), new Vector2(720f, 420f));
		DrawRect(panel, new Color(0.01f, 0.01f, 0.02f, 0.96f), true);
		DrawRect(panel, Colors.White, false, 2f);

		float x = panel.Position.X + 26f;
		float y = panel.Position.Y + 38f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), _runFailed ? "行动失败" : "撤离完成", HorizontalAlignment.Left, -1f, 24, Colors.White);
		y += 40f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"总回合数：{_turn}", HorizontalAlignment.Left, -1f, 16, new Color(0.82f, 0.87f, 0.95f));
		y += 24f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), $"带出战利品价值：{_lootValue}", HorizontalAlignment.Left, -1f, 16, new Color(0.95f, 0.86f, 0.48f));
		y += 36f;
		DrawString(ThemeDB.FallbackFont, new Vector2(x, y), "AI 小队总结", HorizontalAlignment.Left, -1f, 16, Colors.White);
		y += 24f;
		foreach (AiSquad squad in _aiSquads)
		{
			string line = $"{squad.Name}：{GetAiIntentLabel(squad.Intent)}，战力 {Mathf.Max(0, squad.Strength)}，战利品 {squad.Loot.Count}";
			DrawString(ThemeDB.FallbackFont, new Vector2(x, y), line, HorizontalAlignment.Left, 620f, 14, new Color(0.82f, 0.86f, 0.92f));
			y += 22f;
		}

		if (_runFailed)
		{
			Rect2 rect = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(140f, 30f));
			DrawButton(rect, "重新开始", new Color(0.26f, 0.45f, 0.62f));
			_buttons.Add(new ButtonDef(rect, "restart"));
			return;
		}

		Rect2 settlementRect = new(new Vector2(panel.Position.X + 26f, panel.End.Y - 54f), new Vector2(164f, 30f));
		DrawButton(settlementRect, "进入结算转移", new Color(0.26f, 0.45f, 0.62f));
		_buttons.Add(new ButtonDef(settlementRect, "open_settlement"));
		Rect2 restartRect = new(new Vector2(settlementRect.End.X + 12f, panel.End.Y - 54f), new Vector2(140f, 30f));
		DrawButton(restartRect, "直接返回", new Color(0.32f, 0.34f, 0.4f));
		_buttons.Add(new ButtonDef(restartRect, "restart"));
	}

	private void DrawSettlementTransfer()
	{
		GetSettlementLayout(out Rect2 panel, out Rect2 stashRect, out Rect2 backpackRect);
		DrawRect(panel, new Color(0.04f, 0.04f, 0.05f, 0.97f), true);
		DrawRect(panel, Colors.White, false, 2f);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(Ui(24f), Ui(34f)), "撤离结算", HorizontalAlignment.Left, -1f, UiFont(24), Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(Ui(24f), Ui(58f)), "左侧是仓库，右侧是本次带回的局外背包。双向拖动后，点完成结算。", HorizontalAlignment.Left, panel.Size.X - Ui(240f), UiFont(12), new Color(0.82f, 0.88f, 0.94f));
		Rect2 finishRect = new(new Vector2(panel.End.X - Ui(156f), panel.Position.Y + Ui(18f)), new Vector2(Ui(132f), Ui(32f)));
		DrawButton(finishRect, "完成结算", new Color(0.28f, 0.48f, 0.34f));
		_buttons.Add(new ButtonDef(finishRect, "finish_settlement"));
		Rect2 autoPackRect = new(new Vector2(panel.End.X - Ui(264f), panel.Position.Y + Ui(18f)), new Vector2(Ui(92f), Ui(32f)));
		DrawButton(autoPackRect, "整理背包", new Color(0.28f, 0.42f, 0.26f));
		_buttons.Add(new ButtonDef(autoPackRect, "auto_pack_settlement"));
		Rect2 transferAllRect = new(new Vector2(panel.End.X - Ui(382f), panel.Position.Y + Ui(18f)), new Vector2(Ui(108f), Ui(32f)));
		DrawButton(transferAllRect, "全部入仓", new Color(0.26f, 0.42f, 0.58f));
		_buttons.Add(new ButtonDef(transferAllRect, "settlement_all_to_stash"));

		DrawRect(stashRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(backpackRect, new Color(0.09f, 0.1f, 0.12f), true);
		DrawRect(stashRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawRect(backpackRect, new Color(0.28f, 0.31f, 0.36f), false, 1.5f);
		DrawString(ThemeDB.FallbackFont, stashRect.Position + new Vector2(Ui(14f), Ui(24f)), "仓库", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);
		DrawString(ThemeDB.FallbackFont, backpackRect.Position + new Vector2(Ui(14f), Ui(24f)), "局外背包", HorizontalAlignment.Left, -1f, UiFont(20), Colors.White);

		Vector2 stashCellSize = new(Ui(22f), Ui(22f));
		Vector2 stashGridOrigin = stashRect.Position + new Vector2(Ui(14f), Ui(46f));
		Rect2 stashGridRect = new(stashGridOrigin, new Vector2(StashGridWidth * stashCellSize.X, StashGridHeight * stashCellSize.Y));
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
		string hoverLabel = "";
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
				hoverLabel = item.Label;
			}

			_buttons.Add(new ButtonDef(itemRect, "select_stash", i));
		}

		Vector2 cellSize = new(Ui(18f), Ui(18f));
		Vector2 gridOrigin = backpackRect.Position + new Vector2(Ui(14f), Ui(42f));
		List<BackpackCapacityBlock> blocks = BuildHideoutLoadoutCapacityBlocks();
		for (int i = 0; i < blocks.Count; i++)
		{
			BackpackCapacityBlock block = blocks[i];
			Rect2 blockRect = new(
				gridOrigin + new Vector2(block.Cell.X * cellSize.X, block.Cell.Y * cellSize.Y),
				new Vector2(block.Size.X * cellSize.X - Ui(2f), block.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(blockRect, i == 0 ? new Color(0.18f, 0.2f, 0.26f, 0.65f) : new Color(0.16f, 0.18f, 0.22f, 0.6f), true);
			DrawDashedRect(blockRect, new Color(0.82f, 0.86f, 0.94f, 0.4f));
			for (int by = 0; by < block.Size.Y; by++)
			{
				for (int bx = 0; bx < block.Size.X; bx++)
				{
					Rect2 cellRect = new(
						gridOrigin + new Vector2((block.Cell.X + bx) * cellSize.X, (block.Cell.Y + by) * cellSize.Y),
						cellSize - new Vector2(Ui(2f), Ui(2f)));
					DrawRect(cellRect, new Color(0.08f, 0.09f, 0.11f), true);
					DrawRect(cellRect, new Color(0.2f, 0.23f, 0.28f), false, 1f);
				}
			}
		}

		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			BackpackItem item = _hideoutLoadout[i];
			Rect2 itemRect = new(
				gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
				new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
			DrawRect(itemRect, GetGridRarityColor(item.Rarity), true);
			DrawRect(itemRect, i == _selectedSettlementIndex ? new Color(1f, 0.94f, 0.62f, 0.96f) : new Color(1f, 1f, 1f, 0.8f), false, i == _selectedSettlementIndex ? 2f : 1f);
			if (itemRect.Size.X >= Ui(42f))
			{
				DrawString(ThemeDB.FallbackFont, itemRect.Position + new Vector2(Ui(3f), Ui(14f)), item.Label, HorizontalAlignment.Left, itemRect.Size.X - Ui(4f), UiFont(10), Colors.Black);
			}

			if (itemRect.HasPoint(mouse))
			{
				hoverLabel = item.Label;
			}

			_buttons.Add(new ButtonDef(itemRect, "select_settlement", i));
		}

		DrawString(ThemeDB.FallbackFont, new Vector2(backpackRect.Position.X + Ui(14f), backpackRect.End.Y - Ui(22f)),
			$"当前保留 { _hideoutLoadout.Count } 件物品，未转入仓库的内容会继续留在局外背包。",
			HorizontalAlignment.Left, backpackRect.Size.X - Ui(28f), UiFont(12), new Color(0.82f, 0.88f, 0.94f));

		if (_hasDraggedHideoutItem && _draggedHideoutItem != null)
		{
			hoverLabel = _draggedHideoutItem.Label;
		}

		if (!string.IsNullOrEmpty(hoverLabel))
		{
			DrawInventoryTooltip(mouse + new Vector2(Ui(14f), Ui(10f)), hoverLabel);
		}
	}

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
	private string GetAiIntentSummary(AiSquad squad) => squad.Intent switch
	{
		AiIntent.Moving => $"前往：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Clearing => $"清理：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Looting => $"搜刮：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Fighting => $"交战：{this.GetRivalName(squad)}",
		AiIntent.Extracting => $"撤离：{this.GetNodeShortName(squad.IntentTargetNodeId)}",
		AiIntent.Extracted => "已撤离",
		AiIntent.Defeated => "已败退",
		_ => "待机",
	};

	private string GetAiNextActionSummary(AiSquad squad)
	{
		if (!squad.IsAlive)
		{
			return squad.Intent == AiIntent.Extracted ? "已撤离" : "已败退";
		}

		if (squad.BusyTurns > 1)
		{
			return squad.Intent switch
			{
				AiIntent.Clearing => $"继续清图：{this.GetNodeShortName(squad.NodeId)}",
				AiIntent.Looting => $"继续搜刮：{this.GetNodeShortName(squad.NodeId)}",
				AiIntent.Fighting => $"继续交战：{this.GetRivalName(squad)}",
				_ => "继续当前行动",
			};
		}

		if (squad.BusyTurns == 1)
		{
			return squad.Intent switch
			{
				AiIntent.Clearing => "完成清图",
				AiIntent.Looting => "完成搜刮",
				AiIntent.Fighting => "分出胜负",
				_ => "完成当前行动",
			};
		}

		int nextNodeId = this.GetAiPredictedNextNodeId(squad);
		if (nextNodeId >= 0 && nextNodeId != squad.NodeId)
		{
			return $"移动：{this.GetNodeShortName(nextNodeId)}";
		}

		if (squad.Strength <= 3 || squad.Supplies <= 0 || squad.Loot.Count >= 4)
		{
			int extractId = this.FindExtractNode();
			return squad.NodeId == extractId ? "撤离完成" : $"撤离：{this.GetNodeShortName(extractId)}";
		}

		MapNode node = _nodes[squad.NodeId];
		if (node.Threat > 0)
		{
			return $"清图：{this.GetNodeShortName(node.Id)}";
		}

		if (this.CanAiLootNode(node))
		{
			return $"搜刮：{this.GetNodeShortName(node.Id)}";
		}

		return "待机";
	}

	private int GetAiPredictedNextNodeId(AiSquad squad)
	{
		if (!squad.IsAlive || squad.BusyTurns > 0)
		{
			return -1;
		}

		if (squad.Strength <= 3 || squad.Supplies <= 0 || squad.Loot.Count >= 4)
		{
			int extractId = this.FindExtractNode();
			return squad.NodeId == extractId ? -1 : this.FindNextStep(squad.NodeId, extractId);
		}

		MapNode node = _nodes[squad.NodeId];
		if (node.Threat > 0 || this.CanAiLootNode(node))
		{
			return -1;
		}

		int targetId = this.PickAiTargetNode(squad);
		int nextId = this.FindNextStep(squad.NodeId, targetId);
		return nextId == squad.NodeId ? -1 : nextId;
	}

	private string GetNodeShortName(int nodeId)
	{
		if (nodeId < 0 || nodeId >= _nodes.Count)
		{
			return "未知";
		}

		return _nodes[nodeId].Name;
	}

	private string GetRivalName(AiSquad squad)
	{
		if (squad.RivalId < 0 || squad.RivalId >= _aiSquads.Count)
		{
			return "未知";
		}

		return _aiSquads[squad.RivalId].Name;
	}
}
