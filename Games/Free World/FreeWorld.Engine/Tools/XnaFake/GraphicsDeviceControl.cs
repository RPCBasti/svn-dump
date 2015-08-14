using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Xna.Framework.Graphics;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace FreeWorld.Engine.Tools.XnaFake {
	/// <summary>
	/// Custom control uses the XNA Framework GraphicsDevice to render onto
	/// a Windows Form. Derived classes can override the Initialize and Draw
	/// methods to add their own drawing code.
	/// </summary>
	abstract public class GraphicsDeviceControl : Control {
		/// <summary>
		/// However many GraphicsDeviceControl instances you have, they all share
		/// the same underlying GraphicsDevice, managed by this helper service.
		/// </summary>
		protected GraphicsDeviceService mGraphicsDeviceService;

		[DefaultValue(GraphicsProfile.Reach)]
		public GraphicsProfile TargetGraphicsProfile {
			get;
			set;
		}

		/// <summary>
		/// Gets an IServiceProvider containing our IGraphicsDeviceService.
		/// This can be used with components such as the ContentManager,
		/// which use this service to look up the GraphicsDevice.
		/// </summary>
		public ServiceContainer Services {
			get;
			private set;
		}

		/// <summary>
		/// Gets a GraphicsDevice that can be used to draw onto this control.
		/// </summary>
		[Browsable(false)]
		public GraphicsDevice GraphicsDevice {
			get { return mGraphicsDeviceService.GraphicsDevice; }
		}


		protected GraphicsDeviceControl() {
			Services = new ServiceContainer();
		}


		/// <summary>
		/// Derived classes override this to initialize their drawing code.
		/// </summary>
		protected abstract void Initialize();


		/// <summary>
		/// Derived classes override this to draw themselves using the GraphicsDevice.
		/// </summary>
		protected abstract void Draw();


		/// <summary>
		/// Initializes the control.
		/// </summary>
		protected override void OnCreateControl() {
			// Don't initialize the graphics device if we are running in the designer.
			if (!DesignMode) {
				mGraphicsDeviceService = GraphicsDeviceService.AddRef(Handle, ClientSize.Width, ClientSize.Height, TargetGraphicsProfile);

				// Register the service, so components like ContentManager can find it.
				Services.AddService<IGraphicsDeviceService>(mGraphicsDeviceService);

				// Give derived classes a chance to initialize themselves.
				Initialize();
			}

			base.OnCreateControl();
		}


		/// <summary>
		/// Disposes the control.
		/// </summary>
		protected override void Dispose(bool disposing) {
			if (mGraphicsDeviceService != null) {
				mGraphicsDeviceService.Release(disposing);
				mGraphicsDeviceService = null;
			}

			base.Dispose(disposing);
		}


		/// <summary>
		/// Redraws the control in response to a WinForms paint message.
		/// </summary>
		protected override void OnPaint(PaintEventArgs e) {
			string beginDrawError = BeginDraw();

			if (string.IsNullOrEmpty(beginDrawError)) {
				// Draw the control using the GraphicsDevice.
				Draw();
				EndDraw();
			} else {
				// If BeginDraw failed, show an error message using System.Drawing.
				PaintUsingSystemDrawing(e.Graphics, beginDrawError);
			}
		}


		/// <summary>
		/// Attempts to begin drawing the control. Returns an error message string
		/// if this was not possible, which can happen if the graphics device is
		/// lost, or if we are running inside the Form designer.
		/// </summary>
		protected string BeginDraw() {
			// If we have no graphics device, we must be running in the designer.
			if (mGraphicsDeviceService == null) {
				return Text + "\n\n" + GetType();
			}

			// Make sure the graphics device is big enough, and is not lost.
			var deviceResetError = HandleDeviceReset();
			if (string.IsNullOrEmpty(deviceResetError) == false) {
				return deviceResetError;
			}

			// Many GraphicsDeviceControl instances can be sharing the same
			// GraphicsDevice. The device backbuffer will be resized to fit the
			// largest of these controls. But what if we are currently drawing
			// a smaller control? To avoid unwanted stretching, we set the
			// viewport to only use the top left portion of the full backbuffer.
			var viewport = new Viewport {
				X = 0,
				Y = 0,
				Width = ClientSize.Width,
				Height = ClientSize.Height,
				MinDepth = 0,
				MaxDepth = 1
			};

			GraphicsDevice.Viewport = viewport;

			return null;
		}


		/// <summary>
		/// Ends drawing the control. This is called after derived classes
		/// have finished their Draw method, and is responsible for presenting
		/// the finished image onto the screen, using the appropriate WinForms
		/// control handle to make sure it shows up in the right place.
		/// </summary>
		protected void EndDraw() {
			try {
				var sourceRectangle = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
				GraphicsDevice.Present(sourceRectangle, null, Handle);
			} catch {
				// Present might throw if the device became lost while we were
				// drawing. The lost device will be handled by the next BeginDraw,
				// so we just swallow the exception.
			}
		}


		/// <summary>
		/// Helper used by BeginDraw. This checks the graphics device status,
		/// making sure it is big enough for drawing the current control, and
		/// that the device is not lost. Returns an error string if the device
		/// could not be reset.
		/// </summary>
		protected string HandleDeviceReset() {
			bool deviceNeedsReset;

			switch (GraphicsDevice.GraphicsDeviceStatus) {
				case GraphicsDeviceStatus.Lost:
					// If the graphics device is lost, we cannot use it at all.
					return "Graphics device lost";

				case GraphicsDeviceStatus.NotReset:
					// If device is in the not-reset state, we should try to reset it.
					deviceNeedsReset = true;
					break;

				default:
					// If the device state is ok, check whether it is big enough.
					PresentationParameters pp = GraphicsDevice.PresentationParameters;

					deviceNeedsReset = (ClientSize.Width > pp.BackBufferWidth) || (ClientSize.Height > pp.BackBufferHeight);
					break;
			}

			// Do we need to reset the device?
			if (deviceNeedsReset) {
				try {
					mGraphicsDeviceService.ResetDevice(ClientSize.Width, ClientSize.Height);
				} catch (Exception e) {
					return "Graphics device reset failed\n\n" + e;
				}
			}

			return null;
		}


		/// <summary>
		/// If we do not have a valid graphics device (for instance if the device
		/// is lost, or if we are running inside the Form designer), we must use
		/// regular System.Drawing method to display a status message.
		/// </summary>
		protected virtual void PaintUsingSystemDrawing(Graphics graphics, string text) {
			graphics.Clear(Color.CornflowerBlue);

			using (Brush brush = new SolidBrush(Color.Black)) {
				using (var format = new StringFormat()) {
					format.Alignment = StringAlignment.Center;
					format.LineAlignment = StringAlignment.Center;

					graphics.DrawString(text, Font, brush, ClientRectangle, format);
				}
			}
		}


		/// <summary>
		/// Ignores WinForms paint-background messages. The default implementation
		/// would clear the control to the current background color, causing
		/// flickering when our OnPaint implementation then immediately draws some
		/// other color over the top using the XNA Framework GraphicsDevice.
		/// </summary>
		protected override void OnPaintBackground(PaintEventArgs pevent) {

		}

	}

}