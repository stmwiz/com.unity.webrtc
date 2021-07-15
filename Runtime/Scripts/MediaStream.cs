using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.WebRTC
{
    public delegate void DelegateOnAddTrack(MediaStreamTrackEvent e);
    public delegate void DelegateOnRemoveTrack(MediaStreamTrackEvent e);

#if UNITY_WEBGL
    public class MediaStreamConstraints
    {
        public bool audio = true;
        public bool video = true;
    }
#endif

    public class MediaStream : IDisposable
    {
        private DelegateOnAddTrack onAddTrack;
        private DelegateOnRemoveTrack onRemoveTrack;

        private IntPtr self;
        private bool disposed;
        private HashSet<MediaStreamTrack> cacheTracks = new HashSet<MediaStreamTrack>();

#if UNITY_WEBGL
        public void AddUserMedia(MediaStreamConstraints constraints)
        {
            NativeMethods.MediaStreamAddUserMedia(self, JsonUtility.ToJson(constraints));
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        public string Id =>
            NativeMethods.MediaStreamGetID(GetSelfOrThrow()).AsAnsiStringWithFreeMem();

        ~MediaStream()
        {
            this.Dispose();
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }
            if(self != IntPtr.Zero && !WebRTC.Context.IsNull)
            {
                WebRTC.Context.UnRegisterMediaStreamObserver(this);
                WebRTC.Context.DeleteMediaStream(this);
                WebRTC.Table.Remove(self);
                self = IntPtr.Zero;
            }
            this.disposed = true;
            GC.SuppressFinalize(this);
        }

        public DelegateOnAddTrack OnAddTrack
        {
            get => onAddTrack;
            set
            {
                onAddTrack = value;
            }
        }

        public DelegateOnRemoveTrack OnRemoveTrack
        {
            get => onRemoveTrack;
            set
            {
                onRemoveTrack = value;
            }
        }

        private void StopTrack(MediaStreamTrack track)
        {
            WebRTC.Context.StopMediaStreamTrack(track.GetSelfOrThrow());
        }

        public IEnumerable<VideoStreamTrack> GetVideoTracks()
        {
#if !UNITY_WEBGL
            var buf = NativeMethods.MediaStreamGetVideoTracks(GetSelfOrThrow(), out ulong length);
            return WebRTC.Deserialize(buf, (int)length, ptr => new VideoStreamTrack(ptr));
#else
            var ptr = NativeMethods.MediaStreamGetVideoTracks(GetSelfOrThrow());
            var buf = NativeMethods.ptrToIntPtrArray(ptr);
            return WebRTC.Deserialize(buf, p => new VideoStreamTrack(p));
#endif
        }

        public IEnumerable<AudioStreamTrack> GetAudioTracks()
        {
#if !UNITY_WEBGL
            var buf = NativeMethods.MediaStreamGetAudioTracks(GetSelfOrThrow(), out ulong length);
            return WebRTC.Deserialize(buf, (int)length, ptr => new AudioStreamTrack(ptr));
#else
            var ptr = NativeMethods.MediaStreamGetAudioTracks(GetSelfOrThrow());
            var buf = NativeMethods.ptrToIntPtrArray(ptr);
            return WebRTC.Deserialize(buf, p => new AudioStreamTrack(p));
#endif
        }

        public IEnumerable<MediaStreamTrack> GetTracks()
        {
            return GetAudioTracks().Cast<MediaStreamTrack>().Concat(GetVideoTracks());
        }

        public bool AddTrack(MediaStreamTrack track)
        {
            cacheTracks.Add(track);
            return NativeMethods.MediaStreamAddTrack(GetSelfOrThrow(), track.GetSelfOrThrow());
        }
        public bool RemoveTrack(MediaStreamTrack track)
        {
            cacheTracks.Remove(track);
            return NativeMethods.MediaStreamRemoveTrack(GetSelfOrThrow(), track.GetSelfOrThrow());
        }

#if !UNITY_WEBGL
        public MediaStream() : this(WebRTC.Context.CreateMediaStream(Guid.NewGuid().ToString()))
        {
        }
#else
        public MediaStream() : this(WebRTC.Context.CreateMediaStream())
        {
        }
#endif

        internal IntPtr GetSelfOrThrow()
        {
            if (self == IntPtr.Zero)
            {
                throw new InvalidOperationException("This instance has been disposed.");
            }
            return self;
        }

        internal MediaStream(IntPtr ptr)
        {
            self = ptr;
            WebRTC.Table.Add(self, this);
            WebRTC.Context.RegisterMediaStreamObserver(this);
            WebRTC.Context.MediaStreamRegisterOnAddTrack(this, MediaStreamOnAddTrack);
            WebRTC.Context.MediaStreamRegisterOnRemoveTrack(this, MediaStreamOnRemoveTrack);
        }

        [AOT.MonoPInvokeCallback(typeof(DelegateNativeMediaStreamOnAddTrack))]
        static void MediaStreamOnAddTrack(IntPtr ptr, IntPtr trackPtr)
        {
#if !UNITY_WEBGL
            WebRTC.Sync(ptr, () =>
            {
                if (WebRTC.Table[ptr] is MediaStream stream)
                {
                    var e = new MediaStreamTrackEvent(trackPtr);
                    stream.onAddTrack?.Invoke(e);
                    stream.cacheTracks.Add(e.Track);
                }
            });
#else
            if (WebRTC.Table[ptr] is MediaStream stream)
            {
                var e = new MediaStreamTrackEvent(trackPtr);
                stream.onAddTrack?.Invoke(e);
                stream.cacheTracks.Add(e.Track);
            }
#endif
        }

        [AOT.MonoPInvokeCallback(typeof(DelegateNativeMediaStreamOnRemoveTrack))]
        static void MediaStreamOnRemoveTrack(IntPtr ptr, IntPtr trackPtr)
        {
#if !UNITY_WEBGL
            WebRTC.Sync(ptr, () =>
            {
                if (WebRTC.Table[ptr] is MediaStream stream)
                {
                    var e = new MediaStreamTrackEvent(trackPtr);
                    stream.onRemoveTrack?.Invoke(e);
                    stream.cacheTracks.Remove(e.Track);
                }
            });
#else
            if (WebRTC.Table[ptr] is MediaStream stream)
            {
                var e = new MediaStreamTrackEvent(trackPtr);
                stream.onRemoveTrack?.Invoke(e);
                stream.cacheTracks.Remove(e.Track);
            }
#endif
        }
    }
}
