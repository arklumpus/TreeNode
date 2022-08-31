using System;
using System.Security.Cryptography;

namespace PhyloTree.TreeBuilding
{
    /// <summary>
    /// Represents a thread-safe random number generator.
    /// </summary>
    /// <remarks>Adapted from https://stackoverflow.com/questions/3049467/is-c-sharp-random-number-generator-thread-safe</remarks>
    public class ThreadSafeRandom : Random
    {
        private static Random _globalRandom;
        private static object _globalLock = new object();
        [ThreadStatic] private static Random _local;

        private bool _useGlobalRandom;

        /// <summary>
        /// Initialise a new thread-safe random number generator with the specified seed.
        /// </summary>
        /// <param name="seed">A number used to generate a starting number for the pseudo-random sequence.</param>
        public ThreadSafeRandom(int seed)
        {
            lock (_globalLock)
            {
                _globalRandom = new Random(seed);
                _useGlobalRandom = true;
            }
        }

        /// <summary>
        /// Initialise a new thread-safe random number generator.
        /// </summary>
        public ThreadSafeRandom()
        {
            _useGlobalRandom = false;
        }

        private void InitialiseLocal()
        {
            if (_local == null)
            {
                if (!_useGlobalRandom)
                {
                    byte[] buffer = new byte[4];
                    RandomNumberGenerator.Create().GetBytes(buffer);
                    _local = new Random(BitConverter.ToInt32(buffer, 0));
                }
                else
                {
                    lock (_globalLock)
                    {
                        _local = new Random(_globalRandom.Next());
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override int Next()
        {
            InitialiseLocal();
            return _local.Next();
        }

        /// <inheritdoc/>
        public override int Next(int maxValue)
        {
            InitialiseLocal();
            return _local.Next(maxValue);
        }

        /// <inheritdoc/>
        public override int Next(int minValue, int maxValue)
        {
            InitialiseLocal();
            return _local.Next(minValue, maxValue);
        }

        /// <inheritdoc/>
        public override double NextDouble()
        {
            InitialiseLocal();
            return _local.NextDouble();
        }

        /// <inheritdoc/>
        public override void NextBytes(byte[] buffer)
        {
            InitialiseLocal();
            _local.NextBytes(buffer);
        }
    }
}
