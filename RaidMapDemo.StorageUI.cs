using System.Collections.Generic;
using Godot;

public partial class RaidMapDemo
{
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
				string label = string.IsNullOrEmpty(equipped.Label) || equipped.Taken ? $"{GetEquipmentSlotLabel(equipped.Slot)}：空" : $"{GetEquipmentSlotLabel(equipped.Slot)}：{equipped.Label}";
				DrawString(ThemeDB.FallbackFont, slot.Position + new Vector2(10f, 19f), label, HorizontalAlignment.Left, slot.Size.X - 86f, 12, Colors.White);
				if (!equipped.Taken && !string.IsNullOrEmpty(equipped.Label))
				{
					Rect2 takeRect = new(new Vector2(slot.End.X - 70f, slot.Position.Y + 2f), new Vector2(58f, 24f));
					DrawButton(takeRect, "拿取", new Color(0.26f, 0.42f, 0.58f));
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
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 32f), "兑换搜索次数", HorizontalAlignment.Left, -1f, 20, Colors.White);
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 68f), "当前搜索次数不足。是否花费 1 回合换取 4 次搜索，", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		DrawString(ThemeDB.FallbackFont, panel.Position + new Vector2(22f, 90f), "并立即揭示这个容器的下一件物品？", HorizontalAlignment.Left, 430f, 14, new Color(0.84f, 0.88f, 0.94f));
		Rect2 checkRect = new(new Vector2(panel.Position.X + 24f, panel.Position.Y + 126f), new Vector2(18f, 18f));
		DrawRect(checkRect, new Color(0.08f, 0.08f, 0.1f), true);
		DrawRect(checkRect, Colors.White, false, 1.2f);
		if (_confirmSkipChecked)
		{
			DrawLine(checkRect.Position + new Vector2(3f, 9f), checkRect.Position + new Vector2(7f, 14f), new Color(0.56f, 0.95f, 0.62f), 2f);
			DrawLine(checkRect.Position + new Vector2(7f, 14f), checkRect.Position + new Vector2(15f, 3f), new Color(0.56f, 0.95f, 0.62f), 2f);
		}
		_buttons.Add(new ButtonDef(checkRect, "toggle_confirm_skip"));
		DrawString(ThemeDB.FallbackFont, checkRect.Position + new Vector2(28f, 14f), "本次探索后续不再提示", HorizontalAlignment.Left, -1f, 13, Colors.White);
		Rect2 yesRect = new(new Vector2(panel.Position.X + 24f, panel.End.Y - 46f), new Vector2(90f, 28f));
		Rect2 noRect = new(new Vector2(panel.Position.X + 126f, panel.End.Y - 46f), new Vector2(90f, 28f));
		DrawButton(yesRect, "确认", new Color(0.24f, 0.56f, 0.34f));
		DrawButton(noRect, "取消", new Color(0.38f, 0.22f, 0.22f));
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
}
