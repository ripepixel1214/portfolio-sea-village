#nullable enable
using System;

namespace SeaVillage.Ocean
{
    // 미니맵 안개의 셀 단위 탐험 상태. 씬 전환 간 메모리 유지 + SaveData 영속(영속 티어 — 세션 티어인 VoyageSession 과 구분).
    // 그리드 치수는 Ocean 진입 시 Configure 로 확정.
    // 저장은 마을 등 다른 씬에서도 일어날 수 있어, 데이터를 씬-종속 매니저가 아닌 정적 보관소에 둔다.
    public static class MinimapFogState
    {
        private static int cols;
        private static int rows;
        private static bool[]? explored;   // 길이 cols*rows, true = 탐험됨(밝힘)

        // 로드(Apply)가 Ocean 진입(Configure)보다 먼저 일어날 수 있어, 치수 확정 전 보관하는 대기 페이로드
        private static int pendingCols;
        private static int pendingRows;
        private static byte[]? pendingBits;

        // 상태가 외부(로드/리셋)로 교체됐을 때 통지 → 라이브 렌더러 재구성용
        public static event Action? Changed;

        // Ocean 미니맵 초기화 시 그리드 치수 확정. 대기 페이로드가 치수와 일치하면 복원.
        public static void Configure(int newCols, int newRows)
        {
            if (newCols <= 0 || newRows <= 0) return;

            // 동일 치수 재진입이면 기존 탐험 상태 유지
            if (explored != null && cols == newCols && rows == newRows) return;

            cols = newCols;
            rows = newRows;
            explored = new bool[cols * rows];

            if (pendingBits != null && pendingCols == cols && pendingRows == rows)
                UnpackInto(explored, pendingBits);

            pendingBits = null;
        }

        // 셀 (x,y) 를 탐험 처리. 새로 밝혀졌으면 true.
        public static bool Reveal(int x, int y)
        {
            if (explored == null || x < 0 || y < 0 || x >= cols || y >= rows) return false;
            int idx = y * cols + x;
            if (explored[idx]) return false;
            explored[idx] = true;
            return true;
        }

        public static bool IsExplored(int x, int y)
        {
            if (explored == null || x < 0 || y < 0 || x >= cols || y >= rows) return false;
            return explored[y * cols + x];
        }

        // 현재 탐험 상태를 비트마스크로 직렬화(SaveData 용). 미구성 시 대기 페이로드 패스스루.
        public static byte[] ExportBits(out int outCols, out int outRows)
        {
            byte[] result = Array.Empty<byte>();
            CopyBitsTo(ref result, out outCols, out outRows);
            return result;
        }

        public static void CopyBitsTo(ref byte[] target, out int outCols, out int outRows)
        {
            if (explored == null)
            {
                outCols = pendingCols;
                outRows = pendingRows;
                int pendingLength = pendingBits?.Length ?? 0;
                EnsureTargetLength(ref target, pendingLength);
                if (pendingLength > 0)
                    Array.Copy(pendingBits!, target, pendingLength);
                return;
            }

            outCols = cols;
            outRows = rows;
            int byteLength = (explored.Length + 7) / 8;
            EnsureTargetLength(ref target, byteLength);
            Array.Clear(target, 0, target.Length);
            PackInto(explored, target);
        }

        // SaveData 로부터 탐험 상태 주입. 치수 미확정/불일치면 대기 보관, 일치하면 즉시 반영.
        public static void ImportBits(int inCols, int inRows, byte[]? bits)
        {
            // 빈 데이터 = 새 게임/미탐험
            if (inCols <= 0 || inRows <= 0 || bits == null || bits.Length == 0)
            {
                if (explored != null) Array.Clear(explored, 0, explored.Length);
                pendingBits = null;
                Changed?.Invoke();
                return;
            }

            if (explored != null && cols == inCols && rows == inRows)
            {
                Array.Clear(explored, 0, explored.Length);
                UnpackInto(explored, bits);
                pendingBits = null;
            }
            else
            {
                // 아직 치수 미확정(혹은 다름) — 다음 Configure 시점에 반영
                pendingCols = inCols;
                pendingRows = inRows;
                pendingBits = bits;
                if (explored != null) Array.Clear(explored, 0, explored.Length);
            }
            Changed?.Invoke();
        }

        // 새 게임 등에서 안개 초기화
        public static void Reset()
        {
            if (explored != null) Array.Clear(explored, 0, explored.Length);
            pendingBits = null;
            pendingCols = pendingRows = 0;
            Changed?.Invoke();
        }

        private static void PackInto(bool[] src, byte[] bits)
        {
            for (int i = 0; i < src.Length; i++)
                if (src[i]) bits[i >> 3] |= (byte)(1 << (i & 7));
        }

        private static void EnsureTargetLength(ref byte[] target, int length)
        {
            if (target == null || target.Length != length)
                target = length == 0 ? Array.Empty<byte>() : new byte[length];
        }

        // 비트마스크 → bool[]
        private static void UnpackInto(bool[] dst, byte[] bits)
        {
            int n = Math.Min(dst.Length, bits.Length * 8);
            for (int i = 0; i < n; i++)
                dst[i] = (bits[i >> 3] & (1 << (i & 7))) != 0;
        }
    }
}
