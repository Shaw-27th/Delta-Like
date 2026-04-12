using System.Collections.Generic;
using Godot;

public partial class RaidMapDemo
{
	private bool AddLoot(string item)
	{
		return AddLoot(item, GetBackpackItemSize(item), true);
	}

	private bool TryAddToStash(string item)
	{
		return TryAddToStash(CreateBackpackItem(item, GetBackpackItemSize(item)));
	}

	private bool TryAddToStash(BackpackItem item)
	{
		BackpackItem stashItem = CloneBackpackItem(item);
		if (!TryPlaceStorageItem(stashItem, _stash, StashGridWidth, StashGridHeight))
		{
			return false;
		}

		_stash.Add(stashItem);
		return true;
	}

	private bool CanFitItemsInStash(List<BackpackItem> items)
	{
		List<BackpackItem> staged = new();
		for (int i = 0; i < _stash.Count; i++)
		{
			staged.Add(CloneBackpackItem(_stash[i]));
		}

		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem item = CloneBackpackItem(items[i]);
			if (!TryPlaceStorageItem(item, staged, StashGridWidth, StashGridHeight))
			{
				return false;
			}

			staged.Add(item);
		}

		return true;
	}

	private bool CanFitItemsInHideoutLoadout(List<BackpackItem> items)
	{
		List<BackpackItem> staged = new();
		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem item = CloneBackpackItem(items[i]);
			if (!TryPlaceHideoutLoadoutItemInList(item, staged))
			{
				return false;
			}

			staged.Add(item);
		}

		return true;
	}

	private void RebuildHideoutLoadout(List<BackpackItem> items)
	{
		_hideoutLoadout.Clear();
		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem item = CloneBackpackItem(items[i]);
			if (TryPlaceHideoutLoadoutItemInList(item, _hideoutLoadout))
			{
				_hideoutLoadout.Add(item);
			}
		}

		RefreshLootValueFromCurrentInventory();
	}

	private bool HandleOpenContainerPopupClick(Vector2 click)
	{
		if (!_hasDraggedBackpackItem || _selectedContainerIndex < 0 || _inHideout || _runEnded)
		{
			return false;
		}

		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex >= node.Containers.Count)
		{
			return false;
		}

		LootContainer container = node.Containers[_selectedContainerIndex];
		GetContainerPopupGridLayout(container, out Rect2 panel, out Vector2 gridOrigin, out Vector2 cellSize);
		if (!panel.HasPoint(click))
		{
			return false;
		}

		Rect2 gridRect = new(gridOrigin, new Vector2(container.GridSize.X * cellSize.X, container.GridSize.Y * cellSize.Y));
		if (!gridRect.HasPoint(click))
		{
			return true;
		}

		Vector2I cell = GetDraggedBackpackTargetCell(gridOrigin, cellSize);
		if (!TryMoveDraggedBackpackItemToContainer(container, cell))
		{
			_status = "容器空间不足，无法放回。";
		}

		return true;
	}

	private bool TryMoveDraggedBackpackItemToContainer(LootContainer container, Vector2I cell)
	{
		if (!_hasDraggedBackpackItem || _draggedBackpackItem == null)
		{
			return false;
		}

		GridLootItem movedItem = new()
		{
			Label = _draggedBackpackItem.Label,
			Rarity = _draggedBackpackItem.Rarity,
			Size = _draggedBackpackItem.Size,
			Cell = cell,
			AcquiredInRun = _draggedBackpackItem.AcquiredInRun,
			Revealed = true,
			SearchTime = GetGridSearchTime(_draggedBackpackItem.Rarity),
		};

		bool placed = false;
		if (cell.X >= 0 && cell.Y >= 0
			&& cell.X + movedItem.Size.X <= container.GridSize.X
			&& cell.Y + movedItem.Size.Y <= container.GridSize.Y
			&& IsGridAreaFree(container, cell, movedItem.Size))
		{
			movedItem.Cell = cell;
			placed = true;
		}
		else if (movedItem.Size.X != movedItem.Size.Y)
		{
			Vector2I rotated = new(movedItem.Size.Y, movedItem.Size.X);
			if (cell.X >= 0 && cell.Y >= 0
				&& cell.X + rotated.X <= container.GridSize.X
				&& cell.Y + rotated.Y <= container.GridSize.Y
				&& IsGridAreaFree(container, cell, rotated))
			{
				movedItem.Size = rotated;
				movedItem.Cell = cell;
				placed = true;
			}
		}

		if (!placed)
		{
			if (!TryPlaceGridItem(container, movedItem))
			{
				if (movedItem.Size.X == movedItem.Size.Y)
				{
					return false;
				}

				movedItem.Size = new Vector2I(movedItem.Size.Y, movedItem.Size.X);
				if (!TryPlaceGridItem(container, movedItem))
				{
					return false;
				}
			}
		}

		container.GridItems.Add(movedItem);
		LogEvent($"将 {_draggedBackpackItem.Label} 放回了 {container.Label}。");
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
		RefreshStatus();
		return true;
	}

	private bool AddLoot(string item, Vector2I size)
	{
		return AddLoot(item, size, true);
	}

	private bool AddLoot(string item, Vector2I size, bool acquiredInRun)
	{
		BackpackItem backpackItem = CreateBackpackItem(item, size, acquiredInRun);
		if (TryPlaceBackpackItem(backpackItem))
		{
			_runBackpack.Add(backpackItem);
			RefreshLootValueFromCurrentInventory();
			return true;
		}

		_overflowBackpackItems.Add(backpackItem);
		AutoOrganizeBackpack();
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private BackpackItem CreateBackpackItem(string item, Vector2I size, bool acquiredInRun = false)
	{
		return new BackpackItem
		{
			Label = item,
			Rarity = GetItemRarityByLabel(item),
			Size = size,
			AcquiredInRun = acquiredInRun,
		};
	}

	private Vector2I GetBackpackItemSize(string item)
	{
		if (item.Contains("锁甲") || item.Contains("护甲") || item.Contains("武装"))
		{
			return new Vector2I(2, 2);
		}

		if (item.Contains("长枪"))
		{
			return new Vector2I(1, 3);
		}

		if (item.Contains("军刀") || item.Contains("佩刀"))
		{
			return new Vector2I(1, 2);
		}

		if (item.Contains("包") || item.Contains("口粮") || item.Contains("草药"))
		{
			return new Vector2I(2, 1);
		}

		return new Vector2I(1, 1);
	}

	private bool TryPlaceStorageItem(BackpackItem item, List<BackpackItem> items, int gridWidth, int gridHeight)
	{
		if (TryPlaceStorageItemWithSize(item, item.Size, items, gridWidth, gridHeight))
		{
			return true;
		}

		if (item.Size.X != item.Size.Y)
		{
			Vector2I rotated = new(item.Size.Y, item.Size.X);
			if (TryPlaceStorageItemWithSize(item, rotated, items, gridWidth, gridHeight))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryPlaceStorageItemWithSize(BackpackItem item, Vector2I size, List<BackpackItem> items, int gridWidth, int gridHeight)
	{
		for (int y = 0; y <= gridHeight - size.Y; y++)
		{
			for (int x = 0; x <= gridWidth - size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsStorageAreaFree(cell, size, items))
				{
					continue;
				}

				item.Size = size;
				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsStorageAreaFree(Vector2I cell, Vector2I size, List<BackpackItem> items)
	{
		for (int i = 0; i < items.Count; i++)
		{
			BackpackItem existing = items[i];
			bool overlapX = cell.X < existing.Cell.X + existing.Size.X && cell.X + size.X > existing.Cell.X;
			bool overlapY = cell.Y < existing.Cell.Y + existing.Size.Y && cell.Y + size.Y > existing.Cell.Y;
			if (overlapX && overlapY)
			{
				return false;
			}
		}

		return true;
	}

	private bool TryPlaceHideoutLoadoutItemInList(BackpackItem item, List<BackpackItem> items)
	{
		List<BackpackCapacityBlock> blocks = BuildHideoutLoadoutCapacityBlocks();
		for (int y = 0; y <= TeamBackpackMaxRows - item.Size.Y; y++)
		{
			for (int x = 0; x <= TeamBackpackMaxWidth - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsAreaEnabledInBlocks(cell, item.Size, blocks) || !IsStorageAreaFree(cell, item.Size, items))
				{
					continue;
				}

				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool TryPlaceBackpackItem(BackpackItem item)
	{
		for (int y = 0; y <= TeamBackpackMaxRows - item.Size.Y; y++)
		{
			for (int x = 0; x <= TeamBackpackMaxWidth - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsBackpackAreaEnabled(cell, item.Size) || !IsBackpackAreaFree(cell, item.Size))
				{
					continue;
				}

				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsBackpackAreaEnabled(Vector2I cell, Vector2I size)
	{
		for (int y = cell.Y; y < cell.Y + size.Y; y++)
		{
			for (int x = cell.X; x < cell.X + size.X; x++)
			{
				if (!IsCellEnabledInBlocks(new Vector2I(x, y), BuildCurrentBackpackCapacityBlocks()))
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool IsBackpackCellEnabled(Vector2I cell)
	{
		return IsCellEnabledInBlocks(cell, BuildCurrentBackpackCapacityBlocks());
	}

	private bool IsCellEnabledInBlocks(Vector2I cell, List<BackpackCapacityBlock> blocks)
	{
		foreach (BackpackCapacityBlock block in blocks)
		{
			if (cell.X >= block.Cell.X && cell.X < block.Cell.X + block.Size.X
				&& cell.Y >= block.Cell.Y && cell.Y < block.Cell.Y + block.Size.Y)
			{
				return true;
			}
		}

		return false;
	}

	private bool IsAreaEnabledInBlocks(Vector2I cell, Vector2I size, List<BackpackCapacityBlock> blocks)
	{
		for (int y = cell.Y; y < cell.Y + size.Y; y++)
		{
			for (int x = cell.X; x < cell.X + size.X; x++)
			{
				if (!IsCellEnabledInBlocks(new Vector2I(x, y), blocks))
				{
					return false;
				}
			}
		}

		return true;
	}

	private bool IsBackpackAreaFree(Vector2I cell, Vector2I size)
	{
		foreach (BackpackItem existing in _runBackpack)
		{
			bool overlapX = cell.X < existing.Cell.X + existing.Size.X && cell.X + size.X > existing.Cell.X;
			bool overlapY = cell.Y < existing.Cell.Y + existing.Size.Y && cell.Y + size.Y > existing.Cell.Y;
			if (overlapX && overlapY)
			{
				return false;
			}
		}

		return true;
	}

	private int GetCurrentCarryUsage()
	{
		int used = 0;
		foreach (BackpackItem item in _runBackpack)
		{
			used += item.Size.X * item.Size.Y;
		}

		foreach (BackpackItem item in _overflowBackpackItems)
		{
			used += item.Size.X * item.Size.Y;
		}

		return used;
	}

	private int GetCurrentCarryLimit()
	{
		int total = 0;
		foreach (BackpackCapacityBlock block in BuildCurrentBackpackCapacityBlocks())
		{
			total += block.Size.X * block.Size.Y;
		}

		return total;
	}

	private List<BackpackCapacityBlock> BuildCurrentBackpackCapacityBlocks()
	{
		List<BackpackCapacityBlock> blocks = new();
		foreach (RoomUnit unit in _roomUnits)
		{
			if (!unit.IsAlive || !unit.IsPlayerSide)
			{
				continue;
			}

			BackpackCapacityBlock block = new()
			{
				SourceLabel = unit.Name,
				Size = unit.IsHero ? new Vector2I(2, 2) : new Vector2I(1, 1),
			};
			blocks.Add(block);
		}

		ArrangeCapacityBlocks(blocks);
		return blocks;
	}

	private List<BackpackCapacityBlock> BuildHideoutLoadoutCapacityBlocks()
	{
		List<BackpackCapacityBlock> blocks = new()
		{
			new BackpackCapacityBlock
			{
				SourceLabel = "Hero",
				Size = new Vector2I(2, 2),
			}
		};

		for (int i = 0; i < _soldierRoster.Count; i++)
		{
			blocks.Add(new BackpackCapacityBlock
			{
				SourceLabel = _soldierRoster[i].Name,
				Size = new Vector2I(1, 1),
			});
		}

		ArrangeCapacityBlocks(blocks);
		return blocks;
	}

	private void ArrangeCapacityBlocks(List<BackpackCapacityBlock> blocks)
	{
		blocks.Sort((a, b) => (b.Size.X * b.Size.Y).CompareTo(a.Size.X * a.Size.Y));
		bool[,] occupied = new bool[TeamBackpackMaxWidth, TeamBackpackMaxRows];
		for (int i = 0; i < blocks.Count; i++)
		{
			BackpackCapacityBlock block = blocks[i];
			if (!TryPlaceCapacityBlock(block, occupied))
			{
				break;
			}
		}
	}

	private bool TryPlaceCapacityBlock(BackpackCapacityBlock block, bool[,] occupied)
	{
		const int groupSize = 4;
		int groupColumns = Mathf.Max(1, TeamBackpackMaxWidth / groupSize);
		int groupRows = Mathf.Max(1, TeamBackpackMaxRows / groupSize);

		for (int groupRow = 0; groupRow < groupRows; groupRow++)
		{
			for (int groupColumn = 0; groupColumn < groupColumns; groupColumn++)
			{
				int originX = groupColumn * groupSize;
				int originY = groupRow * groupSize;
				int occupiedCount = CountCapacityGroupOccupiedCells(occupied, originX, originY, groupSize);
				if (occupiedCount >= groupSize * groupSize)
				{
					continue;
				}

				if (!TryPlaceCapacityBlockInGroup(block, occupied, originX, originY, groupSize))
				{
					continue;
				}

				return true;
			}
		}

		return false;
	}

	private bool TryPlaceCapacityBlockInGroup(BackpackCapacityBlock block, bool[,] occupied, int originX, int originY, int groupSize)
	{
		int maxX = originX + groupSize - block.Size.X;
		int maxY = originY + groupSize - block.Size.Y;
		Vector2I bestCell = new(-1, -1);
		int bestPrimaryScore = int.MaxValue;
		int bestSecondaryScore = int.MaxValue;
		int bestTertiaryScore = int.MaxValue;
		int bestQuaternaryScore = int.MaxValue;
		for (int y = originY; y <= maxY; y++)
		{
			for (int x = originX; x <= maxX; x++)
			{
				if (!IsCapacityBlockAreaFree(occupied, x, y, block.Size))
				{
					continue;
				}

				GetCapacityPlacementScores(occupied, x, y, block.Size, originX, originY, groupSize,
					out int primaryScore, out int secondaryScore, out int tertiaryScore, out int quaternaryScore);
				if (primaryScore > bestPrimaryScore)
				{
					continue;
				}

				if (primaryScore == bestPrimaryScore && secondaryScore > bestSecondaryScore)
				{
					continue;
				}

				if (primaryScore == bestPrimaryScore && secondaryScore == bestSecondaryScore && tertiaryScore > bestTertiaryScore)
				{
					continue;
				}

				if (primaryScore == bestPrimaryScore && secondaryScore == bestSecondaryScore && tertiaryScore == bestTertiaryScore && quaternaryScore >= bestQuaternaryScore)
				{
					continue;
				}

				bestCell = new Vector2I(x, y);
				bestPrimaryScore = primaryScore;
				bestSecondaryScore = secondaryScore;
				bestTertiaryScore = tertiaryScore;
				bestQuaternaryScore = quaternaryScore;
			}
		}

		if (bestCell.X < 0)
		{
			return false;
		}

		block.Cell = bestCell;
		MarkCapacityBlockArea(occupied, block.Cell, block.Size);
		return true;
	}

	private void GetCapacityPlacementScores(bool[,] occupied, int startX, int startY, Vector2I size, int originX, int originY, int groupSize,
		out int primaryScore, out int secondaryScore, out int tertiaryScore, out int quaternaryScore)
	{
		int minUsedX = int.MaxValue;
		int minUsedY = int.MaxValue;
		int maxUsedX = int.MinValue;
		int maxUsedY = int.MinValue;

		for (int y = originY; y < originY + groupSize && y < TeamBackpackMaxRows; y++)
		{
			for (int x = originX; x < originX + groupSize && x < TeamBackpackMaxWidth; x++)
			{
				bool used = occupied[x, y]
					|| (x >= startX && x < startX + size.X && y >= startY && y < startY + size.Y);
				if (!used)
				{
					continue;
				}

				minUsedX = Mathf.Min(minUsedX, x);
				minUsedY = Mathf.Min(minUsedY, y);
				maxUsedX = Mathf.Max(maxUsedX, x);
				maxUsedY = Mathf.Max(maxUsedY, y);
			}
		}

		if (maxUsedX < minUsedX || maxUsedY < minUsedY)
		{
			primaryScore = int.MaxValue;
			secondaryScore = int.MaxValue;
			tertiaryScore = int.MaxValue;
			quaternaryScore = int.MaxValue;
			return;
		}

		int usedWidth = maxUsedX - minUsedX + 1;
		int usedHeight = maxUsedY - minUsedY + 1;
		primaryScore = Mathf.Max(usedWidth, usedHeight);
		secondaryScore = usedWidth * usedHeight;
		tertiaryScore = Mathf.Abs(usedWidth - usedHeight);
		quaternaryScore = (startY - originY) * groupSize + (startX - originX);
	}

	private int CountCapacityGroupOccupiedCells(bool[,] occupied, int originX, int originY, int groupSize)
	{
		int count = 0;
		for (int y = originY; y < originY + groupSize && y < TeamBackpackMaxRows; y++)
		{
			for (int x = originX; x < originX + groupSize && x < TeamBackpackMaxWidth; x++)
			{
				if (occupied[x, y])
				{
					count++;
				}
			}
		}

		return count;
	}

	private bool IsCapacityBlockAreaFree(bool[,] occupied, int startX, int startY, Vector2I size)
	{
		for (int y = startY; y < startY + size.Y; y++)
		{
			for (int x = startX; x < startX + size.X; x++)
			{
				if (x < 0 || y < 0 || x >= TeamBackpackMaxWidth || y >= TeamBackpackMaxRows || occupied[x, y])
				{
					return false;
				}
			}
		}

		return true;
	}

	private void MarkCapacityBlockArea(bool[,] occupied, Vector2I cell, Vector2I size)
	{
		for (int y = cell.Y; y < cell.Y + size.Y; y++)
		{
			for (int x = cell.X; x < cell.X + size.X; x++)
			{
				occupied[x, y] = true;
			}
		}
	}

	private int GetBackpackPreviewGridHeight()
	{
		int maxHeight = 0;
		foreach (BackpackCapacityBlock block in BuildCurrentBackpackCapacityBlocks())
		{
			maxHeight = Mathf.Max(maxHeight, block.Cell.Y + block.Size.Y);
		}

		foreach (BackpackItem item in _runBackpack)
		{
			maxHeight = Mathf.Max(maxHeight, item.Cell.Y + item.Size.Y);
		}

		return Mathf.Clamp(Mathf.Max(1, maxHeight), 1, TeamBackpackMaxRows);
	}

	private bool IsOverloaded()
	{
		return _overflowBackpackItems.Count > 0 || GetCurrentCarryUsage() > GetCurrentCarryLimit();
	}

	private int GetBackpackPreviewGridWidth()
	{
		int maxWidth = 0;
		foreach (BackpackCapacityBlock block in BuildCurrentBackpackCapacityBlocks())
		{
			maxWidth = Mathf.Max(maxWidth, block.Cell.X + block.Size.X);
		}

		foreach (BackpackItem item in _runBackpack)
		{
			maxWidth = Mathf.Max(maxWidth, item.Cell.X + item.Size.X);
		}

		return Mathf.Clamp(Mathf.Max(1, maxWidth), 1, TeamBackpackMaxWidth);
	}

	private bool HandleBackpackPreviewClick(Vector2 click)
	{
		GetBackpackPreviewLayout(out Vector2 gridOrigin, out Vector2 cellSize, out int gridWidth, out int gridHeight);
		Rect2 gridRect = new(gridOrigin, new Vector2(gridWidth * cellSize.X, gridHeight * cellSize.Y));
		if (!gridRect.HasPoint(click))
		{
			if (_hasDraggedBackpackItem)
			{
				RevertDraggedBackpackItem();
				return true;
			}

			return false;
		}

		if (_hasDraggedBackpackItem)
		{
			Vector2I cell = GetBackpackCellAtPoint(click, gridOrigin, cellSize);
			if (TryPlaceDraggedBackpackItem(cell))
			{
				return true;
			}

			RevertDraggedBackpackItem();
			return true;
		}

		int itemIndex = GetBackpackItemIndexAtPoint(click, gridOrigin, cellSize);
		if (itemIndex < 0)
		{
			return false;
		}

		_draggedBackpackItem = _runBackpack[itemIndex];
		_draggedBackpackOriginalCell = _draggedBackpackItem.Cell;
		_draggedBackpackGrabOffset = click - GetBackpackItemRect(_draggedBackpackItem, gridOrigin, cellSize).Position;
		_hasDraggedBackpackItem = true;
		_runBackpack.RemoveAt(itemIndex);
		return true;
	}

	private bool TryPlaceDraggedBackpackItem(Vector2I cell)
	{
		if (!_hasDraggedBackpackItem)
		{
			return false;
		}

		if (cell.X < 0 || cell.Y < 0
			|| cell.X + _draggedBackpackItem.Size.X > TeamBackpackMaxWidth
			|| cell.Y + _draggedBackpackItem.Size.Y > TeamBackpackMaxRows)
		{
			return false;
		}

		if (!IsBackpackAreaEnabled(cell, _draggedBackpackItem.Size) || !IsBackpackAreaFree(cell, _draggedBackpackItem.Size))
		{
			return false;
		}

		_draggedBackpackItem.Cell = cell;
		_runBackpack.Add(_draggedBackpackItem);
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
		return true;
	}

	private void RevertDraggedBackpackItem()
	{
		if (!_hasDraggedBackpackItem)
		{
			return;
		}

		_draggedBackpackItem.Cell = _draggedBackpackOriginalCell;
		_runBackpack.Add(_draggedBackpackItem);
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		_draggedBackpackGrabOffset = Vector2.Zero;
		RefreshLootValueFromCurrentInventory();
	}

	private Rect2 GetBackpackItemRect(BackpackItem item, Vector2 gridOrigin, Vector2 cellSize)
	{
		return new Rect2(
			gridOrigin + new Vector2(item.Cell.X * cellSize.X, item.Cell.Y * cellSize.Y),
			new Vector2(item.Size.X * cellSize.X - Ui(2f), item.Size.Y * cellSize.Y - Ui(2f)));
	}

	private int GetBackpackItemIndexAtPoint(Vector2 point, Vector2 gridOrigin, Vector2 cellSize)
	{
		for (int i = _runBackpack.Count - 1; i >= 0; i--)
		{
			BackpackItem item = _runBackpack[i];
			Rect2 itemRect = GetBackpackItemRect(item, gridOrigin, cellSize);
			if (itemRect.HasPoint(point))
			{
				return i;
			}
		}

		return -1;
	}

	private Vector2I GetBackpackCellAtPoint(Vector2 point, Vector2 gridOrigin, Vector2 cellSize)
	{
		Vector2 local = point - gridOrigin;
		return new Vector2I(
			Mathf.FloorToInt(local.X / cellSize.X),
			Mathf.FloorToInt(local.Y / cellSize.Y));
	}

	private Vector2I GetDraggedBackpackTargetCell(Vector2 gridOrigin, Vector2 cellSize)
	{
		Vector2 dragOrigin = GetViewport().GetMousePosition() - _draggedBackpackGrabOffset;
		Vector2 local = dragOrigin - gridOrigin + new Vector2(Ui(1f), Ui(1f));
		return new Vector2I(
			Mathf.FloorToInt(local.X / cellSize.X),
			Mathf.FloorToInt(local.Y / cellSize.Y));
	}

	private void AutoOrganizeBackpack()
	{
		List<BackpackItem> allItems = new();
		foreach (BackpackItem item in _runBackpack)
		{
			allItems.Add(CloneBackpackItem(item));
		}

		foreach (BackpackItem item in _overflowBackpackItems)
		{
			allItems.Add(CloneBackpackItem(item));
		}

		if (_hasDraggedBackpackItem && _draggedBackpackItem != null)
		{
			allItems.Add(CloneBackpackItem(_draggedBackpackItem));
			_hasDraggedBackpackItem = false;
			_draggedBackpackItem = null;
		}

		List<BackpackItem> bestPlaced = new();
		List<BackpackItem> bestOverflow = new(allItems);
		int bestScore = -1;
		List<BackpackItem> candidate = new(allItems);
		List<System.Comparison<BackpackItem>> strategies =
		[
			(a, b) => CompareBackpackItemsByAreaDesc(a, b),
			(a, b) => CompareBackpackItemsByAreaAsc(a, b),
			(a, b) => CompareBackpackItemsByHeightDesc(a, b),
		];

		for (int strategyIndex = 0; strategyIndex < strategies.Count; strategyIndex++)
		{
			List<BackpackItem> ordered = new(candidate);
			ordered.Sort(strategies[strategyIndex]);
			List<BackpackItem> placed = new();
			List<BackpackItem> overflow = new();

			for (int i = 0; i < ordered.Count; i++)
			{
				BackpackItem item = CloneBackpackItem(ordered[i]);
				if (TryPlaceBackpackItemIntoList(item, placed))
				{
					placed.Add(item);
				}
				else
				{
					overflow.Add(item);
				}
			}

			int score = GetPlacedBackpackScore(placed);
			if (score > bestScore || (score == bestScore && overflow.Count < bestOverflow.Count))
			{
				bestScore = score;
				bestPlaced = placed;
				bestOverflow = overflow;
			}
		}

		_runBackpack.Clear();
		_runBackpack.AddRange(bestPlaced);
		_overflowBackpackItems.Clear();
		_overflowBackpackItems.AddRange(bestOverflow);
		RefreshLootValueFromCurrentInventory();
		_status = _overflowBackpackItems.Count > 0 ? "自动整理完成，仍有物品留在待整理区。" : "自动整理完成。";
	}

	private void DropOverflowToGround()
	{
		if (_overflowBackpackItems.Count == 0 || _inHideout || _runEnded)
		{
			return;
		}

		LootContainer discard = GetOrCreateGroundDiscardContainer();
		for (int i = 0; i < _overflowBackpackItems.Count; i++)
		{
			AddBackpackItemToContainer(discard, _overflowBackpackItems[i]);
		}

		_overflowBackpackItems.Clear();
		RefreshLootValueFromCurrentInventory();
		_selectedContainerIndex = _nodes[_playerNodeId].Containers.IndexOf(discard);
		_status = "已将待整理物品丢到当前房间。";
		RefreshStatus();
	}

	private void DropDraggedBackpackItemToGround()
	{
		if (!_hasDraggedBackpackItem || _draggedBackpackItem == null || _inHideout || _runEnded)
		{
			return;
		}

		LootContainer discard = GetOrCreateGroundDiscardContainer();
		AddBackpackItemToContainer(discard, _draggedBackpackItem);
		_hasDraggedBackpackItem = false;
		_draggedBackpackItem = null;
		RefreshLootValueFromCurrentInventory();
		_selectedContainerIndex = _nodes[_playerNodeId].Containers.IndexOf(discard);
		_status = "已将物品丢到当前房间。";
		RefreshStatus();
	}

	private LootContainer GetOrCreateGroundDiscardContainer()
	{
		MapNode node = _nodes[_playerNodeId];
		RoomUnit hero = FindHeroUnit();
		Vector2 dropPosition = hero != null ? ClampToRoom(hero.Position + new Vector2(22f, 0f)) : GetRoomArenaRect().GetCenter();
		for (int i = 0; i < node.Containers.Count; i++)
		{
			LootContainer existing = node.Containers[i];
			if (existing.Label == "临时弃置" && existing.Position.DistanceTo(dropPosition) <= 36f)
			{
				return existing;
			}
		}

		LootContainer discard = new()
		{
			Label = "临时弃置",
			Kind = ContainerKind.CorpsePile,
			Position = dropPosition,
			Tint = new Color(0.88f, 0.58f, 0.28f, 1f),
			AutoOpenRange = 62f,
			GridSize = new Vector2I(6, 4),
		};
		node.Containers.Add(discard);
		return discard;
	}

	private BackpackItem CloneBackpackItem(BackpackItem item)
	{
		return new BackpackItem
		{
			Label = item.Label,
			Rarity = item.Rarity,
			Size = item.Size,
			Cell = item.Cell,
			AcquiredInRun = item.AcquiredInRun,
		};
	}

	private void RefreshLootValueFromCurrentInventory()
	{
		int total = 0;
		if (_runEnded && !_runFailed)
		{
			for (int i = 0; i < _hideoutLoadout.Count; i++)
			{
				if (_hideoutLoadout[i].AcquiredInRun)
				{
					total += GetItemValue(_hideoutLoadout[i].Label);
				}
			}

			if (_hasDraggedHideoutItem && _draggedHideoutItem != null && _draggedHideoutItem.AcquiredInRun)
			{
				total += GetItemValue(_draggedHideoutItem.Label);
			}
		}
		else
		{
			for (int i = 0; i < _runBackpack.Count; i++)
			{
				if (_runBackpack[i].AcquiredInRun)
				{
					total += GetItemValue(_runBackpack[i].Label);
				}
			}

			for (int i = 0; i < _overflowBackpackItems.Count; i++)
			{
				if (_overflowBackpackItems[i].AcquiredInRun)
				{
					total += GetItemValue(_overflowBackpackItems[i].Label);
				}
			}

			if (_hasDraggedBackpackItem && _draggedBackpackItem != null && _draggedBackpackItem.AcquiredInRun)
			{
				total += GetItemValue(_draggedBackpackItem.Label);
			}
		}

		_lootValue = total;
	}

	private static int CompareBackpackItemsByAreaDesc(BackpackItem a, BackpackItem b)
	{
		int areaCompare = (b.Size.X * b.Size.Y).CompareTo(a.Size.X * a.Size.Y);
		if (areaCompare != 0)
		{
			return areaCompare;
		}

		return Mathf.Max(b.Size.X, b.Size.Y).CompareTo(Mathf.Max(a.Size.X, a.Size.Y));
	}

	private static int CompareBackpackItemsByAreaAsc(BackpackItem a, BackpackItem b)
	{
		int areaCompare = (a.Size.X * a.Size.Y).CompareTo(b.Size.X * b.Size.Y);
		if (areaCompare != 0)
		{
			return areaCompare;
		}

		return Mathf.Max(a.Size.X, a.Size.Y).CompareTo(Mathf.Max(b.Size.X, b.Size.Y));
	}

	private static int CompareBackpackItemsByHeightDesc(BackpackItem a, BackpackItem b)
	{
		int heightCompare = b.Size.Y.CompareTo(a.Size.Y);
		if (heightCompare != 0)
		{
			return heightCompare;
		}

		return (b.Size.X * b.Size.Y).CompareTo(a.Size.X * a.Size.Y);
	}

	private int GetPlacedBackpackScore(List<BackpackItem> items)
	{
		int score = 0;
		foreach (BackpackItem item in items)
		{
			score += item.Size.X * item.Size.Y * 100 + 1;
		}

		return score;
	}

	private bool TryPlaceBackpackItemIntoList(BackpackItem item, List<BackpackItem> placedItems)
	{
		if (TryPlaceBackpackItemIntoListWithSize(item, item.Size, placedItems))
		{
			return true;
		}

		if (item.Size.X != item.Size.Y)
		{
			Vector2I rotated = new(item.Size.Y, item.Size.X);
			if (TryPlaceBackpackItemIntoListWithSize(item, rotated, placedItems))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryPlaceBackpackItemIntoListWithSize(BackpackItem item, Vector2I size, List<BackpackItem> placedItems)
	{
		for (int y = 0; y <= TeamBackpackMaxRows - size.Y; y++)
		{
			for (int x = 0; x <= TeamBackpackMaxWidth - size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsBackpackAreaEnabled(cell, size) || !IsBackpackAreaFreeInList(cell, size, placedItems))
				{
					continue;
				}

				item.Size = size;
				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsBackpackAreaFreeInList(Vector2I cell, Vector2I size, List<BackpackItem> items)
	{
		foreach (BackpackItem existing in items)
		{
			bool overlapX = cell.X < existing.Cell.X + existing.Size.X && cell.X + size.X > existing.Cell.X;
			bool overlapY = cell.Y < existing.Cell.Y + existing.Size.Y && cell.Y + size.Y > existing.Cell.Y;
			if (overlapX && overlapY)
			{
				return false;
			}
		}

		return true;
	}

	private int GetItemValue(string item)
	{
		if (item.Contains("遗物") || item.Contains("徽记") || item.Contains("宝石"))
		{
			return 18;
		}

		if (item.Contains("锁甲") || item.Contains("军刀") || item.Contains("长枪"))
		{
			return 12;
		}

		if (item.Contains("包") || item.Contains("口粮"))
		{
			return 7;
		}

		return 5;
	}
}
