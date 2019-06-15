/*
 *  File:   ILavalinkRestClient.cs
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

namespace Lavalink4NET.Rest
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Player;

    /// <summary>
    ///     An interface for a lavalink rest client, that can load tracks from a node.
    /// </summary>
    public interface ILavalinkRestClient : IDisposable
    {
        /// <summary>
        ///     Gets the track for the specified <paramref name="query"/> asynchronously.
        /// </summary>
        /// <param name="query">the track search query</param>
        /// <param name="mode">the track search mode</param>
        /// <param name="noCache">
        ///     a value indicating whether the track should be returned from cache, if it is cached.
        ///     Note this parameter does only take any effect is a cache provider is specified in constructor.
        /// </param>
        /// <returns>the track found for the query</returns>
        Task<LavalinkTrack> GetTrackAsync(string query, SearchMode mode = SearchMode.None, bool noCache = false);

        /// <summary>
        ///     Gets the tracks for the specified <paramref name="query"/> asynchronously.
        /// </summary>
        /// <param name="query">the track search query</param>
        /// <param name="mode">the track search mode</param>
        /// <param name="noCache">
        ///     a value indicating whether the track should be returned from cache, if it is cached.
        ///     Note this parameter does only take any effect is a cache provider is specified in constructor.
        /// </param>
        /// <returns>the tracks found for the query</returns>
        Task<IEnumerable<LavalinkTrack>> GetTracksAsync(string query, SearchMode mode = SearchMode.None, bool noCache = false);

        /// <summary>
        ///     Loads the tracks specified by the <paramref name="query"/> asynchronously.
        /// </summary>
        /// <param name="query">the search query</param>
        /// <param name="mode">the track search mode</param>
        /// <param name="noCache">
        ///     a value indicating whether the track should be returned from cache, if it is cached.
        ///     Note this parameter does only take any effect is a cache provider is specified in constructor.
        /// </param>
        /// <returns>
        ///     a task that represents the asynchronous operation <param>the request response</param>
        /// </returns>
        Task<TrackLoadResponsePayload> LoadTracksAsync(string query, SearchMode mode = SearchMode.None, bool noCache = false);
    }
}