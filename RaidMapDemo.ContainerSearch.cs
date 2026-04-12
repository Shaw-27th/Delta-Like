using Godot;

public partial class RaidMapDemo
{
	private void EnsureContainerGrid(LootContainer container)
	{
		if (container.GridItems.Count > 0)
		{
			return;
		}

		container.GridSize = GetContainerGridSize(container);
		foreach (string item in container.VisibleItems)
		{
			AddGridItem(container, item, true);
		}

		for (int i = container.HiddenIndex; i < container.HiddenItems.Count; i++)
		{
			AddGridItem(container, container.HiddenItems[i], false);
		}

		container.VisibleItems.Clear();
		container.HiddenItems.Clear();
		container.HiddenIndex = 0;
	}

	private Vector2I GetContainerGridSize(LootContainer container) => container.Kind switch
	{
		ContainerKind.Room => new Vector2I(5, 4),
		ContainerKind.CorpsePile => new Vector2I(6, 5),
		ContainerKind.EliteCorpse => new Vector2I(5, 4),
		_ => new Vector2I(5, 4),
	};

	private void AddGridItem(LootContainer container, string label, bool revealed)
	{
		GridLootItem item = CreateGridLootItem(label, revealed);
		if (TryPlaceGridItem(container, item))
		{
			container.GridItems.Add(item);
		}
	}

	private GridLootItem CreateGridLootItem(string label, bool revealed)
	{
		ItemRarity rarity = RollGridRarity(label);
		Vector2I size = rarity switch
		{
			ItemRarity.White => _rng.Randf() < 0.6f ? new Vector2I(1, 1) : new Vector2I(1, 2),
			ItemRarity.Green => _rng.Randf() < 0.5f ? new Vector2I(1, 2) : new Vector2I(2, 1),
			ItemRarity.Blue => _rng.Randf() < 0.5f ? new Vector2I(2, 2) : new Vector2I(1, 3),
			ItemRarity.Purple => new Vector2I(2, 2),
			ItemRarity.Gold => new Vector2I(2, 3),
			_ => Vector2I.One,
		};

		return new GridLootItem
		{
			Label = label,
			Rarity = rarity,
			Size = size,
			AcquiredInRun = true,
			Revealed = revealed,
			SearchTime = GetGridSearchTime(rarity),
		};
	}

	private ItemRarity RollGridRarity(string label)
	{
		if (label.Contains("遗物") || label.Contains("宝石"))
		{
			return ItemRarity.Gold;
		}

		if (label.Contains("军刀") || label.Contains("锁甲") || label.Contains("长枪"))
		{
			return ItemRarity.Blue;
		}

		if (label.Contains("坠饰") || label.Contains("徽记"))
		{
			return ItemRarity.Purple;
		}

		if (label.Contains("口粮") || label.Contains("药"))
		{
			return ItemRarity.Green;
		}

		return ItemRarity.White;
	}

	private float GetGridSearchTime(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 0.8f,
		ItemRarity.Green => 1.15f,
		ItemRarity.Blue => 1.6f,
		ItemRarity.Purple => 2.15f,
		ItemRarity.Gold => 2.9f,
		_ => 1f,
	};

	private int GetTimeSlotCost(ItemRarity rarity) => rarity switch
	{
		ItemRarity.White => 10,
		ItemRarity.Green => 20,
		ItemRarity.Blue => 30,
		ItemRarity.Purple => 40,
		ItemRarity.Gold => 50,
		_ => 10,
	};

	private bool TryPlaceGridItem(LootContainer container, GridLootItem item)
	{
		for (int y = 0; y <= container.GridSize.Y - item.Size.Y; y++)
		{
			for (int x = 0; x <= container.GridSize.X - item.Size.X; x++)
			{
				Vector2I cell = new(x, y);
				if (!IsGridAreaFree(container, cell, item.Size))
				{
					continue;
				}

				item.Cell = cell;
				return true;
			}
		}

		return false;
	}

	private bool IsGridAreaFree(LootContainer container, Vector2I cell, Vector2I size)
	{
		foreach (GridLootItem existing in container.GridItems)
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

	private void UpdateContainerSearch(float delta)
	{
		if (_selectedContainerIndex < 0)
		{
			return;
		}

		MapNode node = _nodes[_playerNodeId];
		if (_selectedContainerIndex < 0 || _selectedContainerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[_selectedContainerIndex];
		if (container.ActiveSearchItemIndex < 0 || container.ActiveSearchItemIndex >= container.GridItems.Count)
		{
			return;
		}

		GridLootItem item = container.GridItems[container.ActiveSearchItemIndex];
		item.SearchProgress += delta;
		if (item.SearchProgress < item.SearchTime)
		{
			return;
		}

		item.Revealed = true;
		item.SearchProgress = item.SearchTime;
		container.ActiveSearchItemIndex = -1;
		AddTimeSlotProgress(GetTimeSlotCost(item.Rarity), item.Label);
		LogEvent($"搜索完成，发现了 {item.Label}。");
		if (_autoSearchEnabled)
		{
			StartSearchOnNextHiddenItem(_selectedContainerIndex);
		}
	}

	private void StartSearchOnNextHiddenItem(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		EnsureContainerGrid(container);
		if (container.ActiveSearchItemIndex >= 0)
		{
			return;
		}

		for (int i = 0; i < container.GridItems.Count; i++)
		{
			GridLootItem item = container.GridItems[i];
			if (item.Revealed || item.Taken)
			{
				continue;
			}

			TrySearch(containerIndex * 100 + i);
			return;
		}
	}

	private void AddTimeSlotProgress(int amount, string itemLabel)
	{
		_timeSlotProgress += Mathf.Max(0, amount);
		if (_timeSlotProgress < 100)
		{
			RefreshStatus();
			return;
		}

		int extraTurns = _timeSlotProgress / 100;
		_timeSlotProgress %= 100;
		AdvanceTurn($"搜索 {itemLabel} 耗费了额外时间。", extraTurns, false);
		RefreshStatus();
	}

	private void TrySearch(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "当前房间不够安全，无法搜索。";
			return;
		}

		int containerIndex = encodedIndex / 100;
		int itemIndex = encodedIndex % 100;
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		EnsureContainerGrid(container);
		if (itemIndex < 0 || itemIndex >= container.GridItems.Count)
		{
			return;
		}

		GridLootItem item = container.GridItems[itemIndex];
		if (item.Revealed || item.Taken)
		{
			_status = "这个容器里没有未检视物品了。";
			return;
		}

		item.SearchProgress = 0f;
		container.ActiveSearchItemIndex = itemIndex;
		LogEvent($"开始搜索 {container.Label} 中的物品。");
		RefreshStatus();
	}

	private void OpenContainer(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		_selectedContainerIndex = containerIndex;
		EnsureContainerGrid(node.Containers[containerIndex]);
		if (_autoSearchEnabled && CanSearch(node))
		{
			StartSearchOnNextHiddenItem(containerIndex);
			return;
		}

		RefreshStatus();
	}

	private void ConfirmSearchExchange(bool confirmed)
	{
		_showSearchConfirm = false;
		if (!confirmed)
		{
			_pendingRevealContainerIndex = -1;
			_status = "已取消兑换搜索次数。";
			return;
		}

		if (_confirmSkipChecked)
		{
			_skipSearchConfirm = true;
		}

		if (_pendingRevealContainerIndex >= 0)
		{
			int targetIndex = _pendingRevealContainerIndex;
			_pendingRevealContainerIndex = -1;
			if (_searchActions <= 0)
			{
				AdvanceTurn($"在 {_nodes[_playerNodeId].Name} 花费时间搜索。", 1, false);
				_searchActions += 4;
			}

			TrySearch(targetIndex);
		}
	}

	private void TakeVisible(int encodedIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "房间仍然危险，暂时不能搜刮。";
			return;
		}

		int containerIndex = encodedIndex / 100;
		int itemIndex = encodedIndex % 100;
		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		if (itemIndex >= 50)
		{
			int equipIndex = itemIndex - 50;
			if (equipIndex < 0 || equipIndex >= container.EquippedItems.Count)
			{
				return;
			}

			EquippedLoot equipped = container.EquippedItems[equipIndex];
			if (equipped.Taken || string.IsNullOrEmpty(equipped.Label))
			{
				return;
			}

			if (!AddLoot(equipped.Label))
			{
				return;
			}

			equipped.Taken = true;
			LogEvent($"从 {container.Label} 取走了 {equipped.Label}。");
			RefreshStatus();
			return;
		}

		if (container.GridItems.Count > 0)
		{
			if (itemIndex < 0 || itemIndex >= container.GridItems.Count)
			{
				return;
			}

			GridLootItem gridItem = container.GridItems[itemIndex];
			if (!gridItem.Revealed || gridItem.Taken)
			{
				return;
			}

			if (!AddLoot(gridItem.Label, gridItem.Size, gridItem.AcquiredInRun))
			{
				return;
			}

			gridItem.Taken = true;
			LogEvent($"从 {container.Label} 取走了 {gridItem.Label}。");
			RefreshStatus();
			return;
		}

		if (itemIndex < 0 || itemIndex >= container.VisibleItems.Count)
		{
			return;
		}

		string item = container.VisibleItems[itemIndex];
		if (!AddLoot(item))
		{
			return;
		}

		container.VisibleItems.RemoveAt(itemIndex);
		LogEvent($"从 {container.Label} 取走了 {item}。");
		RefreshStatus();
	}

	private void TakeAllFromContainer(int containerIndex)
	{
		MapNode node = _nodes[_playerNodeId];
		if (!CanSearch(node))
		{
			_status = "房间仍然危险，暂时不能搜刮。";
			return;
		}

		if (containerIndex < 0 || containerIndex >= node.Containers.Count)
		{
			return;
		}

		LootContainer container = node.Containers[containerIndex];
		int takenCount = 0;

		for (int equipIndex = 0; equipIndex < container.EquippedItems.Count; equipIndex++)
		{
			EquippedLoot equipped = container.EquippedItems[equipIndex];
			if (equipped.Taken || string.IsNullOrEmpty(equipped.Label))
			{
				continue;
			}

			if (!AddLoot(equipped.Label))
			{
				break;
			}

			equipped.Taken = true;
			takenCount++;
		}

		if (container.GridItems.Count > 0)
		{
			for (int itemIndex = 0; itemIndex < container.GridItems.Count; itemIndex++)
			{
				GridLootItem item = container.GridItems[itemIndex];
				if (!item.Revealed || item.Taken)
				{
					continue;
				}

				if (!AddLoot(item.Label, item.Size, item.AcquiredInRun))
				{
					break;
				}

				item.Taken = true;
				takenCount++;
			}
		}
		else
		{
			for (int i = container.VisibleItems.Count - 1; i >= 0; i--)
			{
				string item = container.VisibleItems[i];
				if (!AddLoot(item))
				{
					break;
				}

				container.VisibleItems.RemoveAt(i);
				takenCount++;
			}
		}

		if (takenCount <= 0)
		{
			_status = $"{container.Label} 当前没有可一键拿取的物品。";
			return;
		}

		LogEvent($"从 {container.Label} 一次取走了 {takenCount} 件物品。");
		RefreshStatus();
	}

	private bool CanSearch(MapNode node)
	{
		return !_runEnded && !_inHideout && !HasHostilesInRoom();
	}
}
