using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Collections.Generic;

namespace MovedCell
{
	public class TableSource : UITableViewSource, IReorder
	{
		List<string> tableItems;
		string cellIdentifier = "TableCell";
		protected readonly Action<string, int> Move;

		public TableSource (List<string> items, Action<string, int> move)
		{
			tableItems = items;
			Move = move;
		}
		public override int RowsInSection (UITableView tableview, int section)
		{
			return tableItems.Count;
		}
		public override UITableViewCell GetCell (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
		{
			UITableViewCell cell = tableView.DequeueReusableCell (cellIdentifier);
			// if there are no cells to reuse, create a new one
			if (cell == null)
				cell = new UITableViewCell (UITableViewCellStyle.Default, cellIdentifier);
			cell.TextLabel.Text = tableItems[indexPath.Row];
			return cell;
		}



		public override bool CanMoveRow (UITableView tableView, NSIndexPath indexPath)
		{
			var data = tableItems[indexPath.Row];
			return !string.IsNullOrEmpty(data);
		}

		public override NSIndexPath CustomizeMoveTarget (UITableView tableView, NSIndexPath sourceIndexPath, NSIndexPath proposedIndexPath)
		{
			if (proposedIndexPath == null || sourceIndexPath.Section != proposedIndexPath.Section)
			{
				return sourceIndexPath;
			}
			else
			{
				return proposedIndexPath;
			}
		}

		public object SaveObjectAndInsertBlankRowAtIndexPath (UITableView tableView, NSIndexPath indexPath)
		{
			var item = tableItems[indexPath.Row];

			tableItems[indexPath.Row] = "";
			return item;
		}

		public void FinishReorderingWithObject (object obj, NSIndexPath indexPath)
		{

			tableItems.RemoveAt (indexPath.Row);
			tableItems.Insert (indexPath.Row, (string)obj);

			var item = tableItems [indexPath.Row];
			Move (item, indexPath.Row);
		}

		public void MoveRowAtIndexPath(NSIndexPath fromIndexPath, NSIndexPath toIndexPath)
		{
			var item = tableItems [fromIndexPath.Row];

			tableItems.RemoveAt (fromIndexPath.Row);
			tableItems.Insert (toIndexPath.Row, item);
		}
	}
}


