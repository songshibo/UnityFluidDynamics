#ifndef UTIL
    #define UTIL

    static const int2 offset2d[] =
    {
        int2(-1,-1),
        int2(0,-1),
        int2(1,-1),

        int2(-1,0),
        int2(0,0),
        int2(1,0),
        
        int2(-1,1),
        int2(0,1),
        int2(1,1)
    };

    static const int3 offset3d[] =
    {
        int3(-1,-1,-1),
        int3(0,-1,-1),
        int3(1,-1,-1),
        int3(-1,0,-1),
        int3(0,0,-1),
        int3(1,0,-1),
        int3(-1,1,-1),
        int3(0,1,-1),
        int3(1,1,-1),

        int3(-1,-1,0),
        int3(0,-1,0),
        int3(1,-1,0),
        int3(-1,0,0),
        int3(0,0,0),
        int3(1,0,0),
        int3(-1,1,0),
        int3(0,1,0),
        int3(1,1,0),

        int3(-1,-1,1),
        int3(0,-1,1),
        int3(1,-1,1),
        int3(-1,0,1),
        int3(0,0,1),
        int3(1,0,1),
        int3(-1,1,1),
        int3(0,1,1),
        int3(1,1,1)
    };

    int Idx3D(int x, int y, int z, int N)
    {
        return clamp(x + y * N + z * N * N, 0, N * N * N);
    }

    int Idx3D(int3 idx, int N)
    {
        return clamp(idx.x + idx.y * N + idx.z * N * N, 0, N * N * N);
    }

    int Idx2D(int2 idx, int N)
    {
        return clamp(idx.x + idx.y * N, 0, N * N);
    }

    bool InRange3D(int3 idx, int Min, int Max)
    {
        return idx.x >= Min && idx.x < Max && idx.y >= Min && idx.y < Max && idx.z >= Min && idx.z < Max;
    }

    bool InRange2D(int2 idx, int Min, int Max)
    {
        return idx.x >= Min && idx.x < Max && idx.y >= Min && idx.y < Max;
    }

#endif