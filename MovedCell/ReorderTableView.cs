using System;
using System.Collections.Generic;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreAnimation;
using System.Drawing;
using MonoTouch.CoreGraphics;

namespace MovedCell
{
	public interface IReorder
	{
		object SaveObjectAndInsertBlankRowAtIndexPath(UITableView tableView, NSIndexPath indexPath);
		void MoveRowAtIndexPath(NSIndexPath fromIndexPath, NSIndexPath toIndexPath);
		void FinishReorderingWithObject(object obj, NSIndexPath indexPath);
	}

	public class ReorderTableView : UITableView
	{
		private UILongPressGestureRecognizer longPress;
		private CADisplayLink scrollDisplayLink;
		private float scrollRate;
		private NSIndexPath currentLocationIndexPath;
		private NSIndexPath initialIndexPath;
		private UIImageView draggingView;
		private object savedObject;
		private bool canReorder;
		private float draggingViewOpacity;
		private float draggingRowHeight;

		public ReorderTableView(RectangleF rect) : base(rect)
		{
			longPress = new UILongPressGestureRecognizer(LongPress);
			AddGestureRecognizer(longPress);

			SetCanReorder (true);
			draggingViewOpacity = 1.0f;
		}

		private void SetCanReorder (bool canReorder)
		{
			this.canReorder = canReorder;
			longPress.Enabled = canReorder;
		}

		private void LongPress (UILongPressGestureRecognizer gesture)
		{

			var location = gesture.LocationInView(this);
			NSIndexPath indexPath = IndexPathForRowAtPoint(location);

			int sections = NumberOfSections();
			int rows = 0;
			for(int i = 0; i < sections; i++) {
				rows += NumberOfRowsInSection(i);
			}

			// get out of here if the long press was not on a valid row or our table is empty
			// or the dataSource tableView:canMoveRowAtIndexPath: doesn't allow moving the row
			if (rows == 0 || (gesture.State == UIGestureRecognizerState.Began && indexPath == null) ||
				(gesture.State == UIGestureRecognizerState.Ended && currentLocationIndexPath == null) ||
				(gesture.State == UIGestureRecognizerState.Began &&
					indexPath != null && !Source.CanMoveRow(this, indexPath))) {
				CancelGesture();
				return;
			}

			// started
			if (gesture.State == UIGestureRecognizerState.Began) {
				var cell = this.CellAt(indexPath);
				draggingRowHeight = cell.Frame.Size.Height;
				cell.SetSelected(false, false);
				cell.SetHighlighted(false, false);

				// make an image from the pressed tableview cell

				UIGraphics.BeginImageContextWithOptions(cell.Bounds.Size, false, 0);
				cell.Layer.RenderInContext(UIGraphics.GetCurrentContext());
				var cellImage = UIGraphics.GetImageFromCurrentImageContext();
				UIGraphics.EndImageContext();

				// create and image view that we will drag around the screen
				if (draggingView == null) {
					draggingView = new UIImageView(cellImage);
					AddSubview(draggingView);
					var rect = RectForRowAtIndexPath(indexPath);
					draggingView.Frame = new RectangleF(rect.X, rect.Y, draggingView.Bounds.Width, draggingView.Bounds.Height);

					// add drop shadow to image and lower opacity
					draggingView.Layer.MasksToBounds = false;
					draggingView.Layer.ShadowColor = UIColor.Black.CGColor;
					draggingView.Layer.ShadowOffset = new SizeF(0, 0);
					draggingView.Layer.ShadowRadius = 4.0f;
					draggingView.Layer.ShadowOpacity = 0.7f;
					draggingView.Layer.Opacity = draggingViewOpacity;

					// zoom image towards user
					UIView.BeginAnimations(@"zoom");
					//draggingView.Transform = new MonoTouch.CoreGraphics.CGAffineTransform(1.1f, 1.1f,1.1f,1.1f,1.1f,1.1f);
					draggingView.Center = new PointF(Center.X, location.Y);
					UIView.CommitAnimations();
				}

				BeginUpdates();
				DeleteRows(new NSIndexPath[] {indexPath}, UITableViewRowAnimation.None);
				InsertRows(new NSIndexPath[] {indexPath}, UITableViewRowAnimation.None);
				
							savedObject = ((IReorder)Source).SaveObjectAndInsertBlankRowAtIndexPath(this, indexPath);

				currentLocationIndexPath = indexPath;
				initialIndexPath = indexPath;
				EndUpdates();

				// enable scrolling for cell
				scrollDisplayLink = CADisplayLink.Create (ScrollTableWithCell);
				//scrollDisplayLink = new CADisplayLink ();
				//scrollDisplayLink.PerformSelector(scrollTableWithCell,
				//displayLinkWithTarget:self selector:@selector(scrollTableWithCell:)];
				scrollDisplayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoop.NSDefaultRunLoopMode);        
			}
			// dragging
			else if (gesture.State == UIGestureRecognizerState.Changed) {
				// update position of the drag view
				// don't let it go past the top or the bottom too far
				if (location.Y >= 0 && location.Y <= ContentSize.Height + 30) {
					draggingView.Center = new PointF(Center.X, location.Y);
				}

				var rect = Bounds;
				// adjust rect for content inset as we will use it below for calculating scroll zones
				rect.Size.Height -= ContentInset.Top;
				location = gesture.LocationInView(this);

				UpdateCurrentLocation(gesture);

				// tell us if we should scroll and which direction
				var scrollZoneHeight = rect.Size.Height / 6;
				var bottomScrollBeginning = ContentOffset.Y + ContentInset.Top + rect.Size.Height - scrollZoneHeight;
				var topScrollBeginning = ContentOffset.Y + ContentInset.Top  + scrollZoneHeight;
				// we're in the bottom zone
				if (location.Y >= bottomScrollBeginning) {
					scrollRate = (location.Y - bottomScrollBeginning) / scrollZoneHeight;
				}
				// we're in the top zone
				else if (location.Y <= topScrollBeginning) {
					scrollRate = (location.Y - topScrollBeginning) / scrollZoneHeight;
				}
				else {
					scrollRate = 0;
				}
			}
			// dropped
			else if (gesture.State == UIGestureRecognizerState.Ended) {

				var indexPathTemp = currentLocationIndexPath;

				// remove scrolling CADisplayLink
				scrollDisplayLink.Invalidate();
				scrollDisplayLink = null;
				scrollRate = 0;

				// animate the drag view to the newly hovered cell
				UIView.Animate(0.3, new NSAction(() =>
					{
						var rect = RectForRowAtIndexPath(indexPathTemp);
						//draggingView.Transform =  CGAffineTransform.MakeIdentity();
						draggingView.Frame = new RectangleF(rect.X, rect.Y, draggingView.Bounds.Width, draggingView.Bounds.Height);
					}), new NSAction(() => {
						draggingView.RemoveFromSuperview();

						BeginUpdates();
						DeleteRows(new NSIndexPath[] {indexPathTemp}, UITableViewRowAnimation.None);
						InsertRows(new NSIndexPath[] {indexPathTemp}, UITableViewRowAnimation.None);

						((IReorder)Source).FinishReorderingWithObject(savedObject, indexPathTemp);
		
						// reload the rows that were affected just to be safe
						var visibleRows = IndexPathsForVisibleRows.ToList();
						visibleRows.Remove(indexPathTemp);
						ReloadRows(visibleRows.ToArray(), UITableViewRowAnimation.None);
						EndUpdates();
						currentLocationIndexPath = null;
						draggingView = null;
					}));
			}
		}
		private void UpdateCurrentLocation (UILongPressGestureRecognizer gesture)
		{

			NSIndexPath indexPath = null;
			var location = new PointF();

			// refresh index path
			location  = gesture.LocationInView(this);
			indexPath = IndexPathForRowAtPoint(location);

			indexPath = Source.CustomizeMoveTarget (this, initialIndexPath, indexPath);

//			if (RespondsToSelector(tableView:targetIndexPathForMoveFromRowAtIndexPath:toProposedIndexPath:)]) {
//				indexPath = TargetIndexPathForMoveFromRowAtIndexPath(InitialIndexPath, indexPath);
//			}

			var oldHeight = RectForRowAtIndexPath(currentLocationIndexPath).Size.Height;
			var newHeight = RectForRowAtIndexPath(indexPath).Size.Height;

			if (indexPath != null && !indexPath.Equals(currentLocationIndexPath) && gesture.LocationInView(CellAt(indexPath)).Y > newHeight - oldHeight) {
				BeginUpdates();
				DeleteRows(new NSIndexPath[]{currentLocationIndexPath}, UITableViewRowAnimation.Automatic);
				InsertRows(new NSIndexPath[]{indexPath}, UITableViewRowAnimation.Automatic);

				((IReorder)Source).MoveRowAtIndexPath(currentLocationIndexPath, indexPath);

				currentLocationIndexPath = indexPath;
				EndUpdates();
			}
		}

		private void ScrollTableWithCell ()
		{    
			var gesture = longPress;
			var location  = gesture.LocationInView(this);

			var currentOffset = ContentOffset;
			var newOffset = new PointF(currentOffset.X, currentOffset.Y + scrollRate * 10);

			if (newOffset.Y < -ContentInset.Top) {
				newOffset.Y = -ContentInset.Top;
			} else if (ContentSize.Height + ContentInset.Bottom < Frame.Size.Height) {
				newOffset = currentOffset;
			} else if (newOffset.Y > (ContentSize.Height + ContentInset.Bottom) - Frame.Size.Height) {
				newOffset.Y = (ContentSize.Height + ContentInset.Bottom) - Frame.Size.Height;
			}

			SetContentOffset(newOffset, true);

			if (location.Y >= 0 && location.Y <= ContentSize.Height + 50) {
				draggingView.Center = new PointF(Center.X, location.Y);
			}

			UpdateCurrentLocation(gesture);
		}

		private void CancelGesture() 
		{
			longPress.Enabled = false;
			longPress.Enabled = true;
		}
	}
}
