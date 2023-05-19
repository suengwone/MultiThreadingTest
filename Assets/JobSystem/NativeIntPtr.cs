using System;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;

[NativeContainer]
[NativeContainerSupportsDeallocateOnJobCompletion]
[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeIntPtr : IDisposable
{
    [NativeDisableUnsafePtrRestriction]
    internal unsafe int* m_Buffer;

    internal Allocator m_AllocatorLabel;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    private AtomicSafetyHandle m_Safety;

    [NativeSetClassTypeToNullOnSchedule]
    private DisposeSentinel m_DisposeSentinel;
#endif

    public NativeIntPtr(Allocator allocator, int initialValue = 0)
    {
        if(allocator <= Allocator.None)
        {
            throw new ArgumentException("Allocator must be temp, tempjob or persistent", "allocator");
        }

        m_Buffer = (int*)UnsafeUtility.Malloc(
            sizeof(int),
            UnsafeUtility.AlignOf<int>(),
            allocator
        );

        m_AllocatorLabel = allocator;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
        DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif

        *m_Buffer = initialValue;
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    [BurstDiscard]
    private void RequireReadAccess()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
    }

    [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
    [BurstDiscard]
    private void RequireWriteAccess()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
    }

    [WriteAccessRequired]
    public unsafe void Dispose()
    {
        RequireWriteAccess();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
#if UNITY_2018_3_OR_NEWER
        DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
        DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif

        UnsafeUtility.Free(m_Buffer, m_AllocatorLabel);
        m_Buffer = null;
    }

    public int Value
    {
        get
        {
            RequireReadAccess();
            return *m_Buffer;
        }

        [WriteAccessRequired]
        set
        {
            RequireWriteAccess();
            *m_Buffer = value;
        }
    }

    public bool IsCreated
    {
        get
        {
            return m_Buffer != null;
        }
    }

    [NativeContainer]
    [NativeContainerIsAtomicWriteOnly]
    public struct Parallel
    {
        [NativeDisableUnsafePtrRestriction]
        internal int* m_Buffer;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal AtomicSafetyHandle m_Safety;
#endif

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        internal Parallel(int* value, AtomicSafetyHandle safety)
        {
            m_Buffer = value;
            m_Safety = safety;
        }
#else
        internal Parallel(int* value)
        {
            m_Buffer = value;
        }
#endif
        [WriteAccessRequired]
        public void Increment()
        {
            RequireWriteAccess();
            Interlocked.Increment(ref *m_Buffer);
        }

        [WriteAccessRequired]
        public void Decrement()
        {
            RequireWriteAccess();
            Interlocked.Decrement(ref *m_Buffer);
        }

        [WriteAccessRequired]
        public void Add(int value)
        {
            RequireWriteAccess();
            Interlocked.Add(ref *m_Buffer, value);
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [BurstDiscard]
        private void RequireWriteAccess()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
        }
    }
    
    public Parallel GetParallel()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        Parallel parallel = new Parallel(m_Buffer, m_Safety);
        AtomicSafetyHandle.UseSecondaryVersion(ref parallel.m_Safety);
#else
        Parallel parallel = new Parallel(m_Buffer);
#endif
        return parallel;
    }

}