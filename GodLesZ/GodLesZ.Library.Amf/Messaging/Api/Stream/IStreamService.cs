using System;

namespace GodLesZ.Library.Amf.Messaging.Api.Stream {
	/// <summary>
	/// This interface represents the stream methods that can be called throug RTMP.
	/// </summary>
	[CLSCompliant(false)]
	public interface IStreamService : IScopeService {
		/// <summary>
		/// Creates a stream and returns a corresponding id.
		/// </summary>
		/// <returns>ID of created stream.</returns>
		int createStream();
		/// <summary>
		/// Close the stream but not deallocate the resources.
		/// </summary>
		void closeStream();
		/// <summary>
		/// Close the stream if not been closed. Deallocate the related resources.
		/// </summary>
		/// <param name="streamId">Stream id.</param>
		void deleteStream(int streamId);
		/// <summary>
		/// Called by FME.
		/// </summary>
		/// <param name="streamName">Stream name.</param>
		void releaseStream(string streamName);
		/// <summary>
		/// Delete stream.
		/// </summary>
		/// <param name="connection">Stream capable connection.</param>
		/// <param name="streamId">Stream id.</param>
		void deleteStream(IStreamCapableConnection connection, int streamId);
		/// <summary>
		/// Play stream without initial stop.
		/// </summary>
		/// <param name="dontStop">Stoppage flag.</param>
		void play(bool dontStop);
		/// <summary>
		/// Play stream with name.
		/// </summary>
		/// <param name="name">Stream name.</param>
		void play(String name);
		/// <summary>
		/// Play stream with name from start position.
		/// </summary>
		/// <param name="name">Stream name.</param>
		/// <param name="start">Start position.</param>
		void play(String name, double start);
		/// <summary>
		/// Play stream with name from start position and for given amount if time.
		/// </summary>
		/// <param name="name">Stream name.</param>
		/// <param name="start">Start position.</param>
		/// <param name="length">Playback length.</param>
		void play(String name, double start, double length);
		/// <summary>
		/// Publishes stream from given position for given amount of time.
		/// </summary>
		/// <param name="name">Stream name.</param>
		/// <param name="start">Start position.</param>
		/// <param name="length">Playback length.</param>
		/// <param name="flushPlaylist">Flush playlist flag.</param>
		void play(String name, double start, double length, bool flushPlaylist);
		/// <summary>
		/// Publishes stream with given name.
		/// </summary>
		/// <param name="name">Stream published name.</param>
		void publish(String name);
		/// <summary>
		/// Publishes stream with given name and mode.
		/// </summary>
		/// <param name="name">Stream published name.</param>
		/// <param name="mode">Stream publishing mode.</param>
		void publish(String name, String mode);
		/// <summary>
		/// Publish.
		/// </summary>
		/// <param name="dontStop">Whether need to stop first.</param>
		void publish(Boolean dontStop);
		/// <summary>
		/// Seek to position.
		/// </summary>
		/// <param name="position">Seek position.</param>
		void seek(double position);
		/// <summary>
		/// Pauses playback.
		/// </summary>
		/// <param name="pausePlayback">Pause flag.</param>
		/// <param name="position">Pause position.</param>
		void pause(bool pausePlayback, double position);
		/// <summary>
		/// Can recieve video.
		/// </summary>
		/// <param name="receive"></param>
		void receiveVideo(bool receive);
		/// <summary>
		/// Can recieve audio.
		/// </summary>
		/// <param name="receive"></param>
		void receiveAudio(bool receive);
	}
}