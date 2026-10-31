// IO/Audio: the game's sound packs (stored zip of WAVs) and BaseSoundManager semantics
// (instances per sound id, fades, music/sfx volumes) on top of Unity AudioSources.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CastleFrenzy
{
    public class SoundFileManager
    {
        public class Entry
        {
            public string Name;
            public byte[] Pack;
            public int Offset, Size;
            public bool Streamed, Music;
        }

        static SoundFileManager s_instance;
        public static SoundFileManager GetInstance() { return s_instance ?? (s_instance = new SoundFileManager()); }

        public readonly List<Entry> Sounds = new List<Entry>();

        List<KeyValuePair<string, KeyValuePair<int, int>>> ReadZip(byte[] d)
        {
            var r = new List<KeyValuePair<string, KeyValuePair<int, int>>>();
            int pos = 0;
            while (pos + 30 <= d.Length && BitConverter.ToUInt32(d, pos) == 0x04034b50)
            {
                ushort flags = BitConverter.ToUInt16(d, pos + 6);
                int compSize = BitConverter.ToInt32(d, pos + 18);
                int size = BitConverter.ToInt32(d, pos + 22);
                int nameLen = BitConverter.ToUInt16(d, pos + 26);
                int extraLen = BitConverter.ToUInt16(d, pos + 28);
                string name = Encoding.ASCII.GetString(d, pos + 30, nameLen);
                int data = pos + 30 + nameLen + extraLen;
                if ((flags & 8) != 0 && compSize == 0)
                {
                    // sizes follow in a data descriptor; scan for its signature
                    int s = data;
                    while (s + 16 <= d.Length && BitConverter.ToUInt32(d, s) != 0x08074b50) s++;
                    compSize = s - data;
                    size = compSize;
                    pos = s + 16;
                }
                else pos = data + compSize + ((flags & 8) != 0 ? 16 : 0);
                int slash = name.LastIndexOf('/');
                r.Add(new KeyValuePair<string, KeyValuePair<int, int>>(slash >= 0 ? name.Substring(slash + 1) : name, new KeyValuePair<int, int>(data, size)));
            }
            return r;
        }

        void AddPack(string name, bool streamed)
        {
            byte[] d = GameFiles.ReadAllBytes(name);
            if (d == null) { Logs.error("Missing sound pack " + name); return; }
            foreach (var e in ReadZip(d))
                Sounds.Add(new Entry
                {
                    Name = e.Key, Pack = d, Offset = e.Value.Key, Size = e.Value.Value,
                    Streamed = streamed, Music = streamed && e.Key.StartsWith("m_")
                });
        }

        public void AddPack_Memory(string name) { AddPack(name, false); }
        public void AddPack_Stream(string name) { AddPack(name, true); }

        public int GetNumberOfRegisteredSounds() { return Sounds.Count; }

        public int GetSoundIndexFromName(string soundName)
        {
            for (int i = 0; i < Sounds.Count; i++) if (Sounds[i].Name == soundName) return i;
            return -1;
        }

        public string GetSoundNameFromIndex(int id) { return id >= 0 && id < Sounds.Count ? Sounds[id].Name : null; }
    }

    /// <summary>WAV decoding (PCM 8/16 and IMA ADPCM) into Unity clips; ADPCM clips stream.</summary>
    public static class WavDecoder
    {
        static readonly int[] s_stepTable = {
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
            157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552,
            1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487,
            12635, 13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767 };
        static readonly int[] s_indexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };

        public class Format { public int Tag, Channels, Rate, BlockAlign, Bits, SamplesPerBlock; public int DataOffset, DataSize; }

        public static Format Parse(byte[] d, int offset, int size)
        {
            Format f = new Format();
            int pos = offset + 12, end = offset + size;
            while (pos + 8 <= end)
            {
                string id = Encoding.ASCII.GetString(d, pos, 4);
                int sz = BitConverter.ToInt32(d, pos + 4);
                if (id == "fmt ")
                {
                    f.Tag = BitConverter.ToUInt16(d, pos + 8);
                    f.Channels = BitConverter.ToUInt16(d, pos + 10);
                    f.Rate = BitConverter.ToInt32(d, pos + 12);
                    f.BlockAlign = BitConverter.ToUInt16(d, pos + 20);
                    f.Bits = BitConverter.ToUInt16(d, pos + 22);
                    if (f.Tag == 17 && sz >= 20) f.SamplesPerBlock = BitConverter.ToUInt16(d, pos + 26);
                }
                else if (id == "data")
                {
                    f.DataOffset = pos + 8;
                    f.DataSize = Math.Min(sz, end - f.DataOffset);
                }
                pos += 8 + sz + (sz & 1);
            }
            if (f.Tag == 17 && f.SamplesPerBlock == 0 && f.Channels > 0)
                f.SamplesPerBlock = (f.BlockAlign - 4 * f.Channels) * 8 / (4 * f.Channels) + 1;
            return f;
        }

        /// <summary>Decodes one IMA ADPCM block into interleaved floats; returns frames written.</summary>
        public static int DecodeAdpcmBlock(byte[] d, int blockStart, int blockSize, Format f, float[] outBuf)
        {
            int ch = f.Channels;
            int[] pred = new int[ch], idx = new int[ch];
            int p = blockStart;
            for (int c = 0; c < ch; c++)
            {
                pred[c] = BitConverter.ToInt16(d, p);
                idx[c] = Math.Min(88, Math.Max(0, (int)d[p + 2]));
                outBuf[c] = pred[c] / 32768f;
                p += 4;
            }
            int frames = Math.Min(f.SamplesPerBlock, 1 + (blockSize - 4 * ch) * 2 / ch);
            int sample = 1;
            int end = blockStart + blockSize;
            while (sample < frames && p < end)
            {
                for (int c = 0; c < ch; c++)
                {
                    for (int k = 0; k < 4 && p + k < end; k++)
                    {
                        byte b = d[p + k];
                        for (int nib = 0; nib < 2; nib++)
                        {
                            int code = nib == 0 ? (b & 0x0F) : (b >> 4);
                            int step = s_stepTable[idx[c]];
                            int diff = step >> 3;
                            if ((code & 1) != 0) diff += step >> 2;
                            if ((code & 2) != 0) diff += step >> 1;
                            if ((code & 4) != 0) diff += step;
                            if ((code & 8) != 0) pred[c] -= diff; else pred[c] += diff;
                            pred[c] = Math.Max(-32768, Math.Min(32767, pred[c]));
                            idx[c] = Math.Max(0, Math.Min(88, idx[c] + s_indexTable[code]));
                            int s = sample + k * 2 + nib;
                            if (s < frames) outBuf[s * ch + c] = pred[c] / 32768f;
                        }
                    }
                    p += 4;
                }
                sample += 8;
            }
            return frames;
        }

        class AdpcmStream
        {
            public byte[] Data;
            public Format F;
            public int Position;
            float[] m_block;
            int m_blockIndex = -1;

            public void Read(float[] data)
            {
                int ch = F.Channels;
                int frames = data.Length / ch;
                int totalBlocks = F.DataSize / F.BlockAlign + ((F.DataSize % F.BlockAlign) != 0 ? 1 : 0);
                if (m_block == null) m_block = new float[F.SamplesPerBlock * ch];
                for (int i = 0; i < frames; i++)
                {
                    int block = Position / F.SamplesPerBlock, inBlock = Position % F.SamplesPerBlock;
                    if (block >= totalBlocks) { for (int c = 0; c < ch; c++) data[i * ch + c] = 0; Position++; continue; }
                    if (block != m_blockIndex)
                    {
                        Array.Clear(m_block, 0, m_block.Length);
                        int start = F.DataOffset + block * F.BlockAlign;
                        DecodeAdpcmBlock(Data, start, Math.Min(F.BlockAlign, F.DataOffset + F.DataSize - start), F, m_block);
                        m_blockIndex = block;
                    }
                    for (int c = 0; c < ch; c++) data[i * ch + c] = m_block[inBlock * ch + c];
                    Position++;
                }
            }
        }

        public static AudioClip CreateClip(string name, byte[] d, int offset, int size)
        {
            Format f = Parse(d, offset, size);
            if (f.Channels <= 0 || f.DataSize <= 0) return null;
            if (f.Tag == 17)
            {
                int blocks = f.DataSize / f.BlockAlign;
                int rem = f.DataSize % f.BlockAlign;
                int frames = blocks * f.SamplesPerBlock + (rem > 4 * f.Channels ? 1 + (rem - 4 * f.Channels) * 2 / f.Channels : 0);
                AdpcmStream s = new AdpcmStream { Data = d, F = f };
                return AudioClip.Create(name, frames, f.Channels, f.Rate, true, s.Read, pos => s.Position = pos);
            }
            int bytesPer = f.Bits / 8;
            int count = f.DataSize / bytesPer;
            float[] samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                int p = f.DataOffset + i * bytesPer;
                samples[i] = bytesPer == 1 ? (d[p] - 128) / 128f : BitConverter.ToInt16(d, p) / 32768f;
            }
            AudioClip clip = AudioClip.Create(name, count / f.Channels, f.Channels, f.Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }

    public class CSoundManager
    {
        const int k_maxInstancesOfSameSound = 5;

        class Instance
        {
            public AudioSource Source;
            public bool Playing;
            public float Volume;          // target volume (already multiplied by the group volume)
            public float FadeTime, FadeElapsed;
            public bool FadingIn, FadingOut;
            public bool Paused;
        }

        static CSoundManager s_instance;
        public static CSoundManager GetInstance() { return s_instance ?? (s_instance = new CSoundManager()); }

        GameObject m_root;
        List<Instance>[] m_sounds = new List<Instance>[0];
        AudioClip[] m_clips = new AudioClip[0];
        readonly Stack<AudioSource> m_free = new Stack<AudioSource>();
        float m_musicVolume = 1, m_sfxVolume = 1;
        bool m_initialized;

        SoundFileManager Files { get { return SoundFileManager.GetInstance(); } }

        public void init(string profileName)
        {
            int n = Files.GetNumberOfRegisteredSounds();
            m_sounds = new List<Instance>[n];
            for (int i = 0; i < n; i++) m_sounds[i] = new List<Instance>();
            m_clips = new AudioClip[n];
            if (m_root == null)
            {
                m_root = new GameObject("CastleFrenzy Audio");
                UnityEngine.Object.DontDestroyOnLoad(m_root);
            }
            m_initialized = true;
            CUserProfile profile = Profile.Get();
            float sfx, music;
            profile.Get("sfx_volume", out sfx);
            profile.Get("music_volume", out music);
            setSFxVolume(sfx / 100.0f);
            setMusicVolume(music / 100.0f);
        }

        bool IsMusic(int id) { return Files.Sounds[id].Music; }
        bool IsStreamed(int id) { return Files.Sounds[id].Streamed; }

        AudioClip Clip(int id)
        {
            if (m_clips[id] == null)
            {
                SoundFileManager.Entry e = Files.Sounds[id];
                m_clips[id] = WavDecoder.CreateClip(e.Name, e.Pack, e.Offset, e.Size);
            }
            return m_clips[id];
        }

        AudioSource NewSource()
        {
            if (m_free.Count > 0) return m_free.Pop();
            AudioSource s = m_root.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0;
            return s;
        }

        int getReadySound(int id)
        {
            List<Instance> list = m_sounds[id];
            for (int i = 0; i < list.Count; i++) if (!list[i].Playing) return i;
            if (list.Count < k_maxInstancesOfSameSound)
            {
                list.Add(new Instance { Source = NewSource() });
                return list.Count - 1;
            }
            StopInstance(list[0]);
            return 0;
        }

        public int playEx(int soundId, bool loop, float volume, int fadeInTime, float pitch = 1.0f)
        {
            if (!m_initialized || soundId < 0 || soundId >= m_sounds.Length) return -1;
            if (IsStreamed(soundId) && m_sounds[soundId].Count != 0) return -1;
            AudioClip clip = Clip(soundId);
            if (clip == null) return -1;
            int idx = getReadySound(soundId);
            Instance inst = m_sounds[soundId][idx];
            volume *= IsMusic(soundId) ? m_musicVolume : m_sfxVolume;
            inst.Volume = volume;
            inst.Playing = true;
            inst.Paused = false;
            inst.FadingOut = false;
            inst.FadingIn = fadeInTime > 0;
            inst.FadeTime = fadeInTime;
            inst.FadeElapsed = 0;
            inst.Source.clip = clip;
            inst.Source.loop = loop;
            inst.Source.pitch = pitch;
            inst.Source.volume = inst.FadingIn ? 0 : volume;
            inst.Source.Play();
            return idx;
        }

        public int play(int soundId, bool loop = false, int fadeInTime = 0, float volume = 1.0f) { return playEx(soundId, loop, volume, fadeInTime); }
        public int playInPosition(int soundId, v3f position, bool loop = false, float volume = 1.0f) { return playEx(soundId, loop, volume, 0); }
        public void updatePosition(int soundId, int soundIndex, v3f position) { }

        void StopInstance(Instance inst)
        {
            inst.Source.Stop();
            inst.Playing = false;
            inst.FadingIn = inst.FadingOut = false;
        }

        void StopWithFade(Instance inst, int fadeOutTime)
        {
            if (!inst.Playing) return;
            if (fadeOutTime <= 0) { StopInstance(inst); return; }
            inst.FadingIn = false;
            inst.FadingOut = true;
            inst.FadeTime = fadeOutTime;
            inst.FadeElapsed = 0;
        }

        public void stop(int soundId, int soundIndex, int fadeOutTime)
        {
            if (!m_initialized || soundId < 0 || soundId >= m_sounds.Length) return;
            if (soundIndex >= 0 && soundIndex < m_sounds[soundId].Count) StopWithFade(m_sounds[soundId][soundIndex], fadeOutTime);
        }

        public void stop(int soundId, int fadeOutTime = 0)
        {
            if (!m_initialized || soundId < 0 || soundId >= m_sounds.Length) return;
            foreach (Instance i in m_sounds[soundId]) StopWithFade(i, fadeOutTime);
        }

        public void stopAllSounds()
        {
            if (!m_initialized) return;
            for (int i = 0; i < m_sounds.Length; i++) foreach (Instance s in m_sounds[i]) StopInstance(s);
        }

        public void stopAllMusics()
        {
            if (!m_initialized) return;
            for (int i = 0; i < m_sounds.Length; i++) if (IsMusic(i)) foreach (Instance s in m_sounds[i]) StopInstance(s);
        }

        public void stopAllSfx()
        {
            if (!m_initialized) return;
            for (int i = 0; i < m_sounds.Length; i++) if (!IsMusic(i)) foreach (Instance s in m_sounds[i]) StopInstance(s);
        }

        public bool isSoundPlaying(int soundId, int soundIndex = 0)
        {
            if (!m_initialized || soundId < 0 || soundId >= m_sounds.Length) return false;
            if (soundIndex >= m_sounds[soundId].Count) return false;
            return m_sounds[soundId][soundIndex].Playing && !m_sounds[soundId][soundIndex].FadingOut;
        }

        public int playIfNotPlaying(int soundId, bool loop = false, int fadeInTime = 0)
        {
            if (!isSoundPlaying(soundId)) return play(soundId, loop, fadeInTime);
            return -1;
        }

        public void setVolume(int soundId, int soundIndex, float volume)
        {
            if (soundId < 0 || soundId >= m_sounds.Length || soundIndex < 0 || soundIndex >= m_sounds[soundId].Count) return;
            Instance i = m_sounds[soundId][soundIndex];
            i.Volume = volume * (IsMusic(soundId) ? m_musicVolume : m_sfxVolume);
            if (!i.FadingIn && !i.FadingOut) i.Source.volume = i.Volume;
        }

        public void setPitch(int soundId, int soundIndex, float pitch)
        {
            if (soundId < 0 || soundId >= m_sounds.Length || soundIndex < 0 || soundIndex >= m_sounds[soundId].Count) return;
            m_sounds[soundId][soundIndex].Source.pitch = pitch;
        }

        public float getSFxVolume() { return m_sfxVolume; }
        public float getMusicVolume() { return m_musicVolume; }

        public void setSFxVolume(float volume)
        {
            m_sfxVolume = Mathf.Clamp01(volume);
            if (!m_initialized) return;
            for (int i = 0; i < m_sounds.Length; i++)
                if (!IsMusic(i)) foreach (Instance s in m_sounds[i]) { s.Volume = m_sfxVolume; if (!s.FadingIn && !s.FadingOut) s.Source.volume = s.Volume; }
        }

        public void setMusicVolume(float volume)
        {
            m_musicVolume = Mathf.Clamp01(volume);
            if (!m_initialized) return;
            for (int i = 0; i < m_sounds.Length; i++)
                if (IsMusic(i)) foreach (Instance s in m_sounds[i]) { s.Volume = m_musicVolume; if (!s.FadingIn && !s.FadingOut) s.Source.volume = s.Volume; }
        }

        public void pauseAll(bool pause)
        {
            for (int i = 0; i < m_sounds.Length; i++)
                foreach (Instance s in m_sounds[i])
                {
                    if (!s.Playing) continue;
                    if (pause) { s.Source.Pause(); s.Paused = true; }
                    else if (s.Paused) { s.Source.UnPause(); s.Paused = false; }
                }
        }

        public void update(float dtMs)
        {
            if (!m_initialized) return;
            for (int i = 0; i < m_sounds.Length; i++)
            {
                List<Instance> list = m_sounds[i];
                for (int j = 0; j < list.Count; j++)
                {
                    Instance s = list[j];
                    if (s.Playing && !s.Paused)
                    {
                        if (s.FadingIn || s.FadingOut)
                        {
                            s.FadeElapsed += dtMs;
                            float k = Mathf.Clamp01(s.FadeElapsed / Math.Max(1f, s.FadeTime));
                            if (s.FadingIn)
                            {
                                s.Source.volume = s.Volume * k;
                                if (k >= 1) s.FadingIn = false;
                            }
                            else
                            {
                                s.Source.volume = s.Volume * (1 - k);
                                if (k >= 1) StopInstance(s);
                            }
                        }
                        if (s.Playing && !s.Source.isPlaying && !s.Source.loop) s.Playing = false;
                    }
                    if (!s.Playing && j == list.Count - 1)
                    {
                        m_free.Push(s.Source);
                        list.RemoveAt(j);
                    }
                }
            }
        }

        public int GetSoundIndexFromName(string name) { return string.IsNullOrEmpty(name) ? -1 : Files.GetSoundIndexFromName(name); }
        public string GetSoundNameFromIndex(int id) { return Files.GetSoundNameFromIndex(id); }
    }
}
