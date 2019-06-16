/*
 *  File:   LavalinkPlayer.cs
 *  Author: Angelo Breuer
 *
 *  The MIT License (MIT)
 *
 *  Copyright (c) Angelo Breuer 2019
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in
 *  all copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 *  THE SOFTWARE.
 */

namespace Lavalink4NET.Player
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Events;
    using Payloads;

    /// <summary>
    ///     Controls a remote Lavalink Audio Player.
    /// </summary>
    public class LavalinkPlayer : IDisposable
    {
        private readonly LavalinkSocket _lavalinkSocket;
        private DateTimeOffset _lastPositionUpdate;
        private TimeSpan _position;
        private VoiceServer _voiceServer;
        private VoiceState _voiceState;

        /// <summary>
        ///     Initializes a new instance of the <see cref="LavalinkPlayer"/> class.
        /// </summary>
        /// <param name="lavalinkSocket">the lavalink socket</param>
        /// <param name="client">the discord client</param>
        /// <param name="guildId">the identifier of the guild that is controlled by the player</param>
        public LavalinkPlayer(LavalinkSocket lavalinkSocket, IDiscordClientWrapper client, ulong guildId)
        {
            GuildId = guildId;
            Client = client;

            _lavalinkSocket = lavalinkSocket;
            _lastPositionUpdate = DateTimeOffset.UtcNow;
            _position = TimeSpan.Zero;
        }

        /// <summary>
        ///     Gets the default equalizer bands. (All 15 [0-14] equalizer bands set to zero gain)
        /// </summary>
        public static IReadOnlyCollection<EqualizerBand> DefaultEqualizer { get; }
            = Enumerable.Range(0, 15).Select(s => new EqualizerBand(s, 0)).ToList().AsReadOnly();

        /// <summary>
        ///     Gets the discord client wrapper.
        /// </summary>
        public IDiscordClientWrapper Client { get; }

        /// <summary>
        ///     Gets the track that is currently playing.
        /// </summary>
        public LavalinkTrack CurrentTrack { get; private set; }

        /// <summary>
        ///     Gets the identifier snowflake value of the guild the player is for.
        /// </summary>
        public ulong GuildId { get; }

        /// <summary>
        ///     Gets the current player state.
        /// </summary>
        public PlayerState State { get; private set; }
            = PlayerState.NotConnected;

        /// <summary>
        ///     Gets the current track position.
        /// </summary>
        public TimeSpan TrackPosition => CurrentTrack == null ? TimeSpan.Zero :
            DateTimeOffset.UtcNow - _lastPositionUpdate + _position;

        /// <summary>
        ///     Gets the voice channel id the player is connected to.
        /// </summary>
        public ulong? VoiceChannelId { get; private set; }

        /// <summary>
        ///     Gets the current player volume.
        /// </summary>
        public float Volume { get; private set; } = 1f;

        /// <summary>
        ///     Joins the voice channel specified by <paramref name="voiceChannelId"/> asynchronously.
        /// </summary>
        /// <param name="voiceChannelId">the voice channel identifier to join</param>
        /// <param name="selfDeaf">a value indicating whether the bot user should be self deafened</param>
        /// <param name="selfMute">a value indicating whether the bot user should be self muted</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public virtual async Task ConnectAsync(ulong voiceChannelId, bool selfDeaf = false, bool selfMute = false)
        {
            await Client.SendVoiceUpdateAsync(GuildId, voiceChannelId, selfDeaf, selfMute);
            VoiceChannelId = voiceChannelId;
            State = PlayerState.NotPlaying;
        }

        /// <summary>
        ///     Disconnects the player asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        public virtual async Task DisconnectAsync()
        {
            EnsureConnected();

            await Client.SendVoiceUpdateAsync(GuildId, null);
            VoiceChannelId = null;
            State = PlayerState.NotConnected;
        }

        /// <summary>
        ///     Destroys the player asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        public virtual void Dispose()
        {
            if (State == PlayerState.Destroyed)
            {
                return;
            }

            // destroy player
            State = PlayerState.Destroyed;

            // Disconnect from voice channel and send destroy player payload to the lavalink node
            Task.WhenAll(DisconnectAsync(), _lavalinkSocket.SendPayloadAsync(new PlayerDestroyPayload(GuildId)))
                .GetAwaiter().GetResult();
        }

        /// <summary>
        ///     Pauses the current playing track asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the current playing track is already paused.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task PauseAsync()
        {
            EnsureNotDestroyed();
            EnsureConnected();

            if (State != PlayerState.Playing)
            {
                throw new InvalidOperationException("The current playing track is not playing.");
            }

            await _lavalinkSocket.SendPayloadAsync(new PlayerPausePayload(GuildId, true));
            State = PlayerState.Paused;
        }

        /// <summary>
        ///     Plays the specified <paramref name="track"/> asynchronously.
        /// </summary>
        /// <param name="track">the track to play</param>
        /// <param name="startTime">the track start position</param>
        /// <param name="endTime">the track end position</param>
        /// <param name="noReplace">
        ///     a value indicating whether the track play should be ignored if the same track is
        ///     currently playing
        /// </param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task PlayAsync(LavalinkTrack track, TimeSpan? startTime = null,
            TimeSpan? endTime = null, bool noReplace = false)
        {
            EnsureNotDestroyed();
            EnsureConnected();

            CurrentTrack = track ?? throw new ArgumentNullException(nameof(track));

            await _lavalinkSocket.SendPayloadAsync(new PlayerPlayPayload(GuildId, track.Identifier,
                startTime, endTime, noReplace));

            State = PlayerState.Playing;
        }

        /// <summary>
        ///     Resumes the current playing track asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the current playing track is not paused
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task ResumeAsync()
        {
            EnsureNotDestroyed();
            EnsureConnected();

            if (State != PlayerState.Paused)
            {
                throw new InvalidOperationException("There is no track paused.");
            }

            await _lavalinkSocket.SendPayloadAsync(new PlayerPausePayload(GuildId, false));
            State = PlayerState.Playing;
        }

        /// <summary>
        ///     Seeks the current playing track asynchronously.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="NotSupportedException">
        ///     thrown if the current playing track does not support seeking.
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual Task SeekPositionAsync(TimeSpan position)
        {
            EnsureNotDestroyed();
            EnsureConnected();

            if (State != PlayerState.Paused && State != PlayerState.Playing)
            {
                throw new InvalidOperationException("There is no track paused or playing.");
            }

            return _lavalinkSocket.SendPayloadAsync(new PlayerSeekPayload(GuildId, position));
        }

        /// <summary>
        ///     Updates the player volume asynchronously.
        /// </summary>
        /// <param name="volume">the player volume (0f - 10f)</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     thrown if the specified <paramref name="volume"/> is out of range (0f - 10f)
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task SetVolumeAsync(float volume = 1f)
        {
            EnsureNotDestroyed();
            EnsureConnected();

            if (volume > 10f || volume < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume), volume, "Volume is out of range (0f - 10f)");
            }

            var payload = new PlayerVolumePayload(GuildId, (int)(volume * 100));
            await _lavalinkSocket.SendPayloadAsync(payload);

            Volume = volume;
        }

        /// <summary>
        ///     Stops playing the current track asynchronously.
        /// </summary>
        /// <param name="disconnect">
        ///     a value indicating whether the connection to the voice server should be closed
        /// </param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual async Task StopAsync(bool disconnect = false)
        {
            EnsureNotDestroyed();
            EnsureConnected();

            await _lavalinkSocket.SendPayloadAsync(new PlayerStopPayload(GuildId));

            if (disconnect)
            {
                await DisconnectAsync();
            }
        }

        /// <summary>
        ///     Updates the player equalizer asynchronously.
        /// </summary>
        /// <param name="bands">the bands</param>
        /// <param name="reset">
        ///     a value indicating whether the equalizer bands should be overridden (
        ///     <see langword="false"/>) or replaced ( <see langword="true"/>).
        /// </param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        public virtual Task UpdateEqualizerAsync(IEnumerable<EqualizerBand> bands, bool reset = true)
        {
            EnsureNotDestroyed();

            if (reset)
            {
                bands = bands.Union(DefaultEqualizer, new EqualizerBandComparer());
            }

            return _lavalinkSocket.SendPayloadAsync(new PlayerEqualizerPayload(GuildId, bands.ToArray()));
        }

        /// <summary>
        ///     Updates the voice server and sends the data to the Lavalink Node if the voice state
        ///     is also provided.
        /// </summary>
        /// <param name="voiceServer">the voice server data</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        internal Task UpdateAsync(VoiceServer voiceServer)
        {
            EnsureNotDestroyed();

            _voiceServer = voiceServer;
            return UpdateAsync();
        }

        /// <summary>
        ///     Updates the voice state and sends the data to the Lavalink Node if the voice server
        ///     is also provided.
        /// </summary>
        /// <param name="voiceState">the voice state data</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        internal Task UpdateAsync(VoiceState voiceState)
        {
            EnsureNotDestroyed();

            _voiceState = voiceState;
            return UpdateAsync();
        }

        internal void UpdateTrackPosition(DateTimeOffset positionUpdateTime, TimeSpan position)
        {
            _lastPositionUpdate = positionUpdateTime;
            _position = position;
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException"/> when the player is not connected to
        ///     a voice channel.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     thrown if the player is not connected to a voice channel
        /// </exception>
        protected void EnsureConnected()
        {
            if (State == PlayerState.NotConnected)
            {
                throw new InvalidOperationException("The player is not connected to a voice channel.");
            }
        }

        /// <summary>
        ///     Throws an <see cref="InvalidOperationException"/> when the player is destroyed.
        /// </summary>
        /// <exception cref="InvalidOperationException">thrown if the player is destroyed</exception>
        protected void EnsureNotDestroyed()
        {
            if (State == PlayerState.Destroyed)
            {
                throw new InvalidOperationException("The player is destroyed.");
            }
        }

        /// <summary>
        ///     Asynchronously triggered when the player has connected to a voice channel.
        /// </summary>
        /// <param name="voiceServer">the voice server connected to</param>
        /// <param name="voiceState">the voice state</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public virtual Task OnConnectedAsync(VoiceServer voiceServer, VoiceState voiceState)
            => Task.CompletedTask;

        /// <summary>
        ///     Asynchronously triggered when a track ends.
        /// </summary>
        /// <param name="eventArgs">the track event arguments</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public virtual Task OnTrackEndAsync(TrackEndEventArgs eventArgs)
            => Task.CompletedTask;

        /// <summary>
        ///     Asynchronously triggered when an exception occurred while playing a track.
        /// </summary>
        /// <param name="eventArgs">the track event arguments</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public virtual Task OnTrackExceptionAsync(TrackExceptionEventArgs eventArgs)
            => Task.CompletedTask;

        /// <summary>
        ///     Asynchronously triggered when a track got stuck.
        /// </summary>
        /// <param name="eventArgs">the track event arguments</param>
        /// <returns>a task that represents the asynchronous operation</returns>
        public virtual Task OnTrackStuckAsync(TrackStuckEventArgs eventArgs)
            => Task.CompletedTask;

        /// <summary>
        ///     Sends the voice state and server data to the Lavalink Node if both is provided.
        /// </summary>
        /// <returns>a task that represents the asynchronous operation</returns>
        private async Task UpdateAsync()
        {
            if (_voiceServer == null || _voiceState == null)
            {
                return;
            }

            await _lavalinkSocket.SendPayloadAsync(new VoiceUpdatePayload(_voiceState.GuildId,
                _voiceState.VoiceSessionId, new VoiceServerUpdateEvent(_voiceServer)));

            await OnConnectedAsync(_voiceServer, _voiceState);

            _voiceServer = null;
            _voiceState = null;
        }
    }
}