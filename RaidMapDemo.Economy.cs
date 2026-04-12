using System.Collections.Generic;

public partial class RaidMapDemo
{
	private void SellStashItem(int index)
	{
		if (index < 0 || index >= _stash.Count)
		{
			return;
		}

		string item = _stash[index].Label;
		_money += GetItemValue(item);
		_stash.RemoveAt(index);
		_selectedStashIndex = -1;
		_selectedShopIndex = -1;
	}

	private void BuyShopItem(int index)
	{
		if (index < 0 || index >= _shopStock.Count)
		{
			return;
		}

		ShopEntry entry = _shopStock[index];
		if (_money < entry.Price)
		{
			return;
		}

		_money -= entry.Price;
		if (!TryAddToStash(entry.Label))
		{
			_money += entry.Price;
			_status = "仓库空间不足，无法买入。";
			return;
		}

		_selectedShopIndex = -1;
		_status = $"已购入 {entry.Label}。";
	}

	private void MoveAllSettlementItemsToStash()
	{
		if (_hideoutLoadout.Count == 0)
		{
			_status = "局外背包当前没有可转移的物品。";
			return;
		}

		List<BackpackItem> movedItems = new();
		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			movedItems.Add(CloneBackpackItem(_hideoutLoadout[i]));
		}

		if (!CanFitItemsInStash(movedItems))
		{
			_status = "仓库空间不足，无法全部转入。";
			return;
		}

		for (int i = 0; i < movedItems.Count; i++)
		{
			TryAddToStash(movedItems[i]);
		}

		_hideoutLoadout.Clear();
		_selectedSettlementIndex = -1;
		RefreshLootValueFromCurrentInventory();
		_status = "已将局外背包全部转入仓库。";
	}

	private void RepackHideoutLoadout()
	{
		List<BackpackItem> items = new();
		for (int i = 0; i < _hideoutLoadout.Count; i++)
		{
			items.Add(CloneBackpackItem(_hideoutLoadout[i]));
		}

		RebuildHideoutLoadout(items);
	}
}
