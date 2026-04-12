using Godot;

public partial class RaidMapDemo
{
	private void TryMoveToNode(int nodeId)
	{
		MapNode current = _nodes[_playerNodeId];
		if (!current.Links.Contains(nodeId))
		{
			_status = "该节点与当前位置不相连。";
			return;
		}

		_isPlayerMoving = true;
		_plannedExitNodeId = nodeId;
		_moveTargetNodeId = nodeId;
		_moveProgress = 0f;
		_playerMarkerPosition = _nodes[_playerNodeId].Position;
		AdvanceTurn($"移动至 {_nodes[nodeId].Name}。");
		_status = $"正在前往 {_nodes[nodeId].Name}。";
	}

	private void TryPlanExitToNode(int nodeId)
	{
		MapNode current = _nodes[_playerNodeId];
		if (!current.Links.Contains(nodeId))
		{
			_status = "战略地图只能标记相邻节点。";
			return;
		}

		_plannedExitNodeId = nodeId;
		_status = $"已标记出口目标：{_nodes[nodeId].Name}。返回房间后可通过对应出口转场。";
	}

	private void TryUseExit(int nodeId)
	{
		if (_selectedContainerIndex >= 0 || _runEnded || _inHideout)
		{
			return;
		}

		MapNode node = _nodes[_playerNodeId];
		if (!node.Links.Contains(nodeId))
		{
			return;
		}

		if (IsOverloaded())
		{
			_status = "队伍当前超载，无法离开这个房间。";
			return;
		}

		Vector2 direction = GetExitDirection(node, nodeId);
		Rect2 doorRect = GetRoomExitRect(node, nodeId);
		_pendingExitNodeId = nodeId;
		_pendingExitDirection = direction;
		_heroMoveTarget = ClampToRoom(doorRect.GetCenter());
		_heroHasMoveTarget = true;
		_status = $"前往 {_nodes[nodeId].Name} 的门口。";
	}

	private void HandleArrival(MapNode node)
	{
		node.SearchRewardClaimed = true;
		_roomDirty = true;
		RefreshStatus();
		return;
	}

	private void StartEncounter(MapNode node, string enemyName, int enemyPower, AiSquad squad, bool hasElite)
	{
		_roomDirty = true;
		return;
	}

	private void QueueEncounter(MapNode node, string enemyName, int enemyPower, AiSquad squad, bool hasElite, string promptText)
	{
		_roomDirty = true;
		return;
	}

	private void StartPendingEncounter()
	{
		_roomDirty = true;
		return;
	}

	private void OnBattleFinished(bool victory, bool heroAlive, int remainingHp, int remainingSoldiers, int remainingStrength)
	{
		return;
	}

	private void AdvanceTurn(string reason, int amount = 1, bool refreshStatus = true)
	{
		for (int i = 0; i < amount; i++)
		{
			_turn++;
			SimulateAiTurn();
			CheckPlayerNodeEncounterAfterTimeAdvance();
		}

		LogEvent($"第 {_turn} 回合：{reason}");
		if (refreshStatus && !_runEnded)
		{
			RefreshStatus();
		}
	}

	private void SimulateAiTurn()
	{
		ResolveAiMeetings();
		foreach (AiSquad squad in _aiSquads)
		{
			if (!squad.IsAlive)
			{
				continue;
			}

			if (squad.BusyTurns > 0)
			{
				squad.BusyTurns--;
				if (squad.BusyTurns == 0)
				{
					ResolveAiBusyAction(squad);
				}
				continue;
			}

			if (squad.Strength <= 3 || squad.Supplies <= 0 || squad.Loot.Count >= 4)
			{
				int extractId = FindExtractNode();
				if (squad.NodeId == extractId)
				{
					squad.Intent = AiIntent.Extracted;
					squad.IntentTargetNodeId = extractId;
					LogEvent($"{squad.Name} 已撤离，带走了 {squad.Loot.Count} 件战利品。");
				}
				else
				{
					MoveAiToward(squad, extractId, AiIntent.Extracting);
				}
				continue;
			}

			MapNode node = _nodes[squad.NodeId];
			if (node.Threat > 0)
			{
				squad.Intent = AiIntent.Clearing;
				squad.IntentTargetNodeId = node.Id;
				squad.BusyTurns = _rng.RandiRange(1, 2);
				LogEvent($"{squad.Name} 开始清理 {node.Name}。");
				continue;
			}

			if (CanAiLootNode(node))
			{
				squad.Intent = AiIntent.Looting;
				squad.IntentTargetNodeId = node.Id;
				squad.BusyTurns = 1;
				LogEvent($"{squad.Name} 开始搜刮 {node.Name}。");
				continue;
			}

			MoveAiToward(squad, PickAiTargetNode(squad), AiIntent.Moving);
		}
		ResolveAiMeetings();
	}

	private void ResolveAiBusyAction(AiSquad squad)
	{
		MapNode node = _nodes[squad.NodeId];
		if (squad.Intent == AiIntent.Clearing)
		{
			int loss = _rng.RandiRange(0, 2);
			squad.Strength = Mathf.Max(1, squad.Strength - loss);
			node.Threat = 0;
			LogEvent($"{squad.Name} 清理了 {node.Name}，损失了 {loss} 点战力。");
		}
		else if (squad.Intent == AiIntent.Looting)
		{
			if (TryAiLootNode(squad, node))
			{
				LogEvent($"{squad.Name} 搜刮了 {node.Name}。");
			}
		}
		else if (squad.Intent == AiIntent.Fighting)
		{
			ResolveAiDuel(squad);
			return;
		}

		squad.Intent = AiIntent.Idle;
		squad.IntentTargetNodeId = -1;
	}

	private void ResolveAiMeetings()
	{
		for (int i = 0; i < _aiSquads.Count; i++)
		{
			AiSquad a = _aiSquads[i];
			if (!a.IsAlive || a.BusyTurns > 0 || a.NodeId == _playerNodeId)
			{
				continue;
			}

			for (int j = i + 1; j < _aiSquads.Count; j++)
			{
				AiSquad b = _aiSquads[j];
				if (!b.IsAlive || b.BusyTurns > 0 || b.NodeId != a.NodeId)
				{
					continue;
				}

				a.Intent = AiIntent.Fighting;
				b.Intent = AiIntent.Fighting;
				a.IntentTargetNodeId = a.NodeId;
				b.IntentTargetNodeId = b.NodeId;
				a.BusyTurns = _rng.RandiRange(3, 5);
				b.BusyTurns = a.BusyTurns;
				a.RivalId = j;
				b.RivalId = i;
				LogEvent($"{a.Name} 与 {b.Name} 在 {_nodes[a.NodeId].Name} 交战。");
			}
		}
	}

	private void CheckPlayerNodeEncounterAfterTimeAdvance()
	{
		if (_runEnded || _inHideout)
		{
			return;
		}

		_roomDirty = true;
	}

	private void ResolveAiDuel(AiSquad squad)
	{
		if (squad.RivalId < 0 || squad.RivalId >= _aiSquads.Count)
		{
			squad.Intent = AiIntent.Idle;
			squad.IntentTargetNodeId = -1;
			return;
		}

		AiSquad rival = _aiSquads[squad.RivalId];
		if (!rival.IsAlive)
		{
			squad.Intent = AiIntent.Idle;
			squad.IntentTargetNodeId = -1;
			squad.RivalId = -1;
			return;
		}

		if (_aiSquads.IndexOf(squad) > squad.RivalId)
		{
			return;
		}

		AiSquad winner = squad.Strength + _rng.RandiRange(0, 4) >= rival.Strength + _rng.RandiRange(0, 4) ? squad : rival;
		AiSquad loser = winner == squad ? rival : squad;
		winner.Strength = Mathf.Max(1, winner.Strength - _rng.RandiRange(1, 3));
		winner.Intent = AiIntent.Idle;
		winner.IntentTargetNodeId = -1;
		winner.BusyTurns = 0;
		winner.RivalId = -1;
		loser.Strength = 0;
		loser.Intent = AiIntent.Defeated;
		loser.IntentTargetNodeId = -1;
		loser.BusyTurns = 0;
		loser.RivalId = -1;
		MapNode node = _nodes[winner.NodeId];
		LootContainer pile = new()
		{
			Label = $"{loser.Name} 的遗骸",
			Kind = ContainerKind.CorpsePile,
			Position = ClampToRoom(GetRoomArenaRect().GetCenter()),
			Tint = new Color(0.92f, 0.5f, 0.22f, 1f),
			AutoOpenRange = 58f,
		};
		pile.HiddenItems.Add("破损徽章");
		pile.HiddenItems.Add("野战口粮");
		pile.HiddenItems.Add(RollLootItem());
		node.Containers.Add(pile);
		LogEvent($"{winner.Name} 在 {node.Name} 击败了 {loser.Name}。");
	}

	private bool CanAiLootNode(MapNode node)
	{
		if (node.Id == _playerNodeId || node.Threat > 0)
		{
			return false;
		}

		foreach (LootContainer container in node.Containers)
		{
			if (!container.IsEmpty)
			{
				return true;
			}
		}

		return false;
	}

	private bool TryAiLootNode(AiSquad squad, MapNode node)
	{
		foreach (LootContainer container in node.Containers)
		{
			foreach (EquippedLoot equipped in container.EquippedItems)
			{
				if (equipped.Taken || string.IsNullOrEmpty(equipped.Label))
				{
					continue;
				}

				equipped.Taken = true;
				squad.Loot.Add(equipped.Label);
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}

			if (container.VisibleItems.Count > 0)
			{
				squad.Loot.Add(container.VisibleItems[0]);
				container.VisibleItems.RemoveAt(0);
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}

			if (container.HiddenRemaining > 0)
			{
				squad.Loot.Add(container.HiddenItems[container.HiddenIndex]);
				container.HiddenIndex++;
				squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
				return true;
			}
		}
		return false;
	}

	private int PickAiTargetNode(AiSquad squad)
	{
		int bestId = squad.NodeId;
		float bestScore = float.MinValue;
		foreach (MapNode node in _nodes)
		{
			if (node.Id == squad.NodeId)
			{
				continue;
			}

			float score = 0f;
			score += node.Type == NodeType.Search ? 6f : 0f;
			score += node.Type == NodeType.Battle ? 3f : 0f;
			score += node.Threat > 0 ? 3f : 0f;
			score += CountNodeLoot(node) * 1.3f;
			score -= _nodes[squad.NodeId].Position.DistanceTo(node.Position) * 0.01f;
			if (node.Id == _playerNodeId)
			{
				score += 1.5f;
			}

			if (score > bestScore)
			{
				bestScore = score;
				bestId = node.Id;
			}
		}
		return bestId;
	}

	private void MoveAiToward(AiSquad squad, int targetId, AiIntent intent)
	{
		int nextId = FindNextStep(squad.NodeId, targetId);
		if (nextId == squad.NodeId)
		{
			squad.IntentTargetNodeId = targetId;
			return;
		}
		squad.NodeId = nextId;
		squad.Intent = intent;
		squad.IntentTargetNodeId = targetId;
		squad.Supplies = Mathf.Max(0, squad.Supplies - 1);
		LogEvent($"{squad.Name} 移动到了 {_nodes[nextId].Name}。");
	}

	private int FindNextStep(int fromId, int targetId)
	{
		if (fromId == targetId)
		{
			return fromId;
		}

		int best = fromId;
		float bestDistance = float.MaxValue;
		foreach (int link in _nodes[fromId].Links)
		{
			float distance = _nodes[link].Position.DistanceTo(_nodes[targetId].Position);
			if (distance < bestDistance)
			{
				bestDistance = distance;
				best = link;
			}
		}
		return best;
	}

	private int FindExtractNode()
	{
		foreach (MapNode node in _nodes)
		{
			if (node.Type == NodeType.Extract)
			{
				return node.Id;
			}
		}
		return 0;
	}

	private AiSquad GetSquadAtNode(int nodeId)
	{
		foreach (AiSquad squad in _aiSquads)
		{
			if (squad.IsAlive && squad.NodeId == nodeId)
			{
				return squad;
			}
		}
		return null;
	}

	private void RefreshStatus()
	{
		MapNode node = _nodes[_playerNodeId];
		AiSquad enemy = GetSquadAtNode(node.Id);
		if (enemy != null)
		{
			_status = $"当前节点存在敌对小队：{enemy.Name}。";
			return;
		}
		if (node.Threat > 0)
		{
			_status = $"{node.Name} 仍有守军驻守。";
			return;
		}
		if (node.Type == NodeType.Extract)
		{
			_status = "这里是撤离点。";
			return;
		}
		_status = $"{node.Name} 当前安全，时隙 {_timeSlotProgress}/100。";
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
