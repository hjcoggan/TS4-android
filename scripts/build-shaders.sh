#!/usr/bin/env bash
# Build the shader pack the Native renderer loads: work/shaderpack/topspin4.shaderpack.
#
# XenosRecomp never runs on the device (HANDOFF decision 2), so this is the only place shaders are
# converted: on the Mac, from the prepared game data, with the same XenosRecomp source, patch
# series and pack format the reference uses (docs/shaders.md).
#
#   ts4-port/scripts/build-shaders.sh [--clean] [--no-convert]
#
# Steps:
#   1. read the Xenos shader containers out of the game data (lzxtool, below):
#        every .arc archive, the same way the Launcher's shader step reads them, and
#        the read-only data of Loader_DLL.xex and Swing_DLL.xex (the game's Module mode's set);
#   2. convert them with XenosRecomp (work/build/xenosrecomp/XenosRecomp/XenosRecomp) to SPIR-V,
#      written as shader_cache.cpp plus one HLSL file per shader;
#   3. repack that as work/shaderpack/topspin4.shaderpack, with each shader's texture fetch slots
#      and the converter id the app's CMakeLists.txt computes from ts4-port/data/xenosrecomp;
#   4. check the pack the way src/native/shader_pack.cpp loads it (magic, sizes, sorted tables,
#      zstd, smol-v).
#
# Everything it writes is game-derived and stays in work/ (never in ts4-port/, ADR-0006). Logs:
# work/logs/shader-*.log.
#
# Overrides: TS4_GAMEDATA (work/gamedata), TS4_SHADERPACK_DIR (work/shaderpack),
# TS4_XENOSRECOMP (work/xenosrecomp, the patched source), TS4_XENOSRECOMP_BUILD
# (work/build/xenosrecomp), TS4_REXGLUE_SDK (work/rexglue-sdk, for libmspack's LZX decoder),
# TS4_PATCHES (ts4-port/data/xenosrecomp).
set -euo pipefail

root=$(cd "$(dirname "$0")/../.." && pwd)
gamedata=${TS4_GAMEDATA:-$root/work/gamedata}
out=${TS4_SHADERPACK_DIR:-$root/work/shaderpack}
xr_src=${TS4_XENOSRECOMP:-$root/work/xenosrecomp}
xr_build=${TS4_XENOSRECOMP_BUILD:-$root/work/build/xenosrecomp}
sdk=${TS4_REXGLUE_SDK:-$root/work/rexglue-sdk}
patches=${TS4_PATCHES:-$root/ts4-port/data/xenosrecomp}
logs=$root/work/logs
cmake=${CMAKE:-/opt/homebrew/bin/cmake}
clang=${CLANG:-/usr/bin/clang}
python=${PYTHON:-python3}

clean=0
convert=1
for argument in "$@"; do
    case $argument in
        --clean) clean=1 ;;
        --no-convert) convert=0 ;;
        -h|--help) sed -n '2,30p' "$0"; exit 0 ;;
        *) echo "unknown argument: $argument" >&2; exit 64 ;;
    esac
done

step() { printf '\n== %s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }
count() { find "$1" -name '*.bin' 2>/dev/null | wc -l | tr -d ' '; }

mkdir -p "$logs" "$out"
[ -d "$gamedata" ] || die "no game data at $gamedata (run ts4-port/scripts/prepare-gamedata.sh)"
[ -d "$xr_src/XenosRecomp" ] || die "no XenosRecomp source at $xr_src (clone zolaware/reblue-XenosRecomp at 339af41, apply ts4-port/data/xenosrecomp/*.patch)"
[ -d "$sdk/thirdparty/libmspack/libmspack/mspack" ] || die "no libmspack in $sdk (run ts4-port/scripts/prepare-sdk.sh)"
[ -d "$patches" ] || die "no XenosRecomp patch series at $patches"
[ "$(ls -1 "$patches"/*.patch 2>/dev/null | wc -l | tr -d ' ')" -gt 0 ] || die "no patches in $patches"

if [ "$clean" = 1 ]; then
    step "clean"
    rm -rf "$out/containers" "$out/converted" "$out/xex" "$out/build" "$out/topspin4.shaderpack"
fi

# ---------------------------------------------------------------------------- 1. the extractor

# lzxtool: the container reader (the Launcher's GameArchive.cs + module_shaders.h rules) over
# libmspack's LZX decoder, which is what ReXGlue's own XEX loader uses. Built here, not shipped.
step "building the container extractor"
mkdir -p "$out/build"
cat > "$out/build/lzxtool.c" <<'LZXTOOL_C'
// Host helper for the Top Spin 4 shader pack: read the Xenos shader containers out of the game's
// data, using libmspack's LZX decoder (the one ReXGlue's XEX loader uses).
//
//   lzxtool arc <archive.arc> <outdir>   containers in a game archive (src GameArchive.cs layout)
//   lzxtool xex <module.xex> <out.img>   decompress an XEX image (XEX normal compression)
//
// Built by ts4-port/scripts/build-shaders.sh; not part of the app.

#include <fcntl.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/mman.h>
#include <sys/stat.h>
#include <unistd.h>

#include <CommonCrypto/CommonCryptor.h>

#include <zstd.h>

#include <lzx.h>
#include <mspack.h>

// ---------------------------------------------------------------- mspack system over memory

typedef struct {
  const uint8_t* data;
  size_t len;
  size_t pos;
} MemBuf;

typedef struct {
  struct mspack_file base;
  MemBuf* in;
  MemBuf* out;
  // Archive frames: the compressed bytes are split into frames with a header between them, which
  // read() skips (GameArchive.Read's framing). next_frame is the body offset of the next header.
  const uint8_t* body;
  size_t body_len;
  size_t at;
  size_t chunk_end;
  int framed;
} MemFile;

static MemFile g_file;

static struct mspack_file* sys_open(struct mspack_system* self, const char* name, int mode) {
  (void)self; (void)name; (void)mode;
  memset(&g_file, 0, sizeof(g_file));
  return (struct mspack_file*)&g_file;
}

static void sys_close(struct mspack_file* file) { (void)file; }

static int sys_read(struct mspack_file* file, void* buffer, int bytes) {
  MemFile* f = (MemFile*)file;
  uint8_t* p = (uint8_t*)buffer;
  int done = 0;
  while (done < bytes) {
    if (!f->framed) {
      size_t left = f->in->len - f->in->pos;
      size_t n = (size_t)(bytes - done) < left ? (size_t)(bytes - done) : left;
      if (!n) break;
      memcpy(p + done, f->in->data + f->in->pos, n);
      f->in->pos += n;
      done += (int)n;
      continue;
    }
    if (f->at >= f->chunk_end) {
      if (f->at >= f->body_len) break;
      size_t at = f->at;
      size_t chunk;
      if (f->body[at] == 0xFF) {
        if (at + 5 > f->body_len) break;
        chunk = (size_t)((f->body[at + 3] << 8) | f->body[at + 4]);
        at += 5;
      } else {
        if (at + 2 > f->body_len) break;
        chunk = (size_t)((f->body[at] << 8) | f->body[at + 1]);
        at += 2;
      }
      if (!chunk || at + chunk > f->body_len) break;
      f->at = at;
      f->chunk_end = at + chunk;
      continue;
    }
    size_t n = (size_t)(bytes - done) < (f->chunk_end - f->at) ? (size_t)(bytes - done)
                                                              : (f->chunk_end - f->at);
    memcpy(p + done, f->body + f->at, n);
    f->at += n;
    done += (int)n;
  }
  return done;
}

static int sys_write(struct mspack_file* file, void* buffer, int bytes) {
  MemFile* f = (MemFile*)file;
  if (!f->out) return -1;
  if (f->out->pos + (size_t)bytes > f->out->len) {
    size_t want = f->out->pos + (size_t)bytes;
    size_t cap = f->out->len ? f->out->len : 0x10000;
    while (cap < want) cap *= 2;
    uint8_t* grown = (uint8_t*)realloc((void*)f->out->data, cap);
    if (!grown) return -1;
    f->out->data = grown;
    f->out->len = cap;
  }
  memcpy((void*)(f->out->data + f->out->pos), buffer, (size_t)bytes);
  f->out->pos += (size_t)bytes;
  return bytes;
}

static int sys_seek(struct mspack_file* file, off_t offset, int mode) {
  MemFile* f = (MemFile*)file;
  if (f->framed || !f->in) return -1;
  off_t base = mode == MSPACK_SYS_SEEK_START ? 0 : (off_t)f->in->pos;
  off_t target = base + offset;
  if (target < 0 || (size_t)target > f->in->len) return -1;
  f->in->pos = (size_t)target;
  return 0;
}

static off_t sys_tell(struct mspack_file* file) {
  MemFile* f = (MemFile*)file;
  return (off_t)(f->framed ? f->at : f->in->pos);
}

static void sys_message(struct mspack_file* file, const char* fmt, ...) { (void)file; (void)fmt; }
static void* sys_alloc(struct mspack_system* self, size_t bytes) { (void)self; return calloc(1, bytes); }
static void sys_free(void* ptr) { free(ptr); }
static void sys_copy(void* src, void* dest, size_t bytes) { memcpy(dest, src, bytes); }

static struct mspack_system g_system = {sys_open, sys_close, sys_read, sys_write, sys_seek, sys_tell,
                                        sys_message, sys_alloc, sys_free, sys_copy, NULL};

// ---------------------------------------------------------------- containers

static uint32_t be32(const uint8_t* p) {
  return (uint32_t)p[0] << 24 | (uint32_t)p[1] << 16 | (uint32_t)p[2] << 8 | p[3];
}
static uint16_t be16(const uint8_t* p) { return (uint16_t)((p[0] << 8) | p[1]); }

// ShaderContainer.IsContainer / module_shaders.h Scan.
static int is_container(const uint8_t* p, size_t len, size_t* size, int* pixel) {
  if (len < 12 || p[0] != 0x10 || p[1] != 0x2A || p[2] != 0x11 || p[3] > 1) return 0;
  uint64_t header = be32(p + 4);
  uint64_t microcode = be32(p + 8);
  uint64_t total = header + microcode;
  if (header < 12 || !microcode || total > (1u << 20) || total > len) return 0;
  *size = (size_t)total;
  *pixel = p[3] == 0;
  return 1;
}

static uint64_t fnv1a64(const uint8_t* p, size_t len) {
  uint64_t h = 14695981039346656037ull;
  for (size_t i = 0; i < len; ++i) h = (h ^ p[i]) * 1099511628211ull;
  return h;
}

static int write_container(const char* outdir, const uint8_t* p, size_t len, int pixel) {
  char path[4096];
  snprintf(path, sizeof(path), "%s/%s_%016llx.bin", outdir, pixel ? "ps" : "vs",
           (unsigned long long)fnv1a64(p, len));
  if (access(path, F_OK) == 0) return 0;  // the same container in two archives
  FILE* f = fopen(path, "wb");
  if (!f) { perror(path); return -1; }
  if (fwrite(p, 1, len, f) != len) { fclose(f); return -1; }
  fclose(f);
  return 1;
}

// ---------------------------------------------------------------- archive mode

typedef struct {
  size_t out_size;
  size_t body_at;  // offset in the entry body of the frame's compressed bytes
  size_t chunk;
} Frame;

static int arc_entry(const uint8_t* body, size_t body_len, size_t size, int window_bits,
                     const char* outdir, int* written) {
  // The frames, from their headers.
  Frame frames[4096];
  size_t count = 0, at = 0, produced = 0;
  while (produced < size) {
    if (count == sizeof(frames) / sizeof(frames[0])) return -1;
    size_t frame, chunk;
    if (at + 2 > body_len) return -1;
    if (body[at] == 0xFF) {
      if (at + 5 > body_len) return -1;
      frame = be16(body + at + 1);
      chunk = be16(body + at + 3);
      at += 5;
    } else {
      frame = 0x8000;
      chunk = be16(body + at);
      at += 2;
    }
    if (produced + frame > size) frame = size - produced;
    if (!frame || !chunk || at + chunk > body_len) return -1;
    frames[count++] = (Frame){frame, at, chunk};
    produced += frame;
    at += chunk;
  }

  MemBuf out = {NULL, 0, 0};
  memset(&g_file, 0, sizeof(g_file));
  g_file.out = &out;
  struct lzxd_stream* lzx = lzxd_init(&g_system, (struct mspack_file*)&g_file,
                                      (struct mspack_file*)&g_file, window_bits, 0, 0x8000,
                                      (off_t)size, 0);
  if (!lzx) return -1;
  int wanted = 0;
  for (size_t i = 0; i < count; ++i) {
    g_file.framed = 1;
    g_file.body = body;
    g_file.body_len = body_len;
    g_file.at = frames[i].body_at;
    g_file.chunk_end = frames[i].body_at + frames[i].chunk;
    int e = lzxd_decompress(lzx, (off_t)frames[i].out_size);
    if (e != MSPACK_ERR_OK) {
      if (getenv("LZXTOOL_DEBUG"))
        fprintf(stderr, "  frame %zu/%zu out %zu chunk %zu: err %d (pos %zu)\n", i, count,
                frames[i].out_size, frames[i].chunk, e, out.pos);
      lzxd_free(lzx);
      free((void*)out.data);
      return -1;
    }
    if (i == 0) {
      size_t csize;
      int pixel;
      if (!is_container(out.data, out.pos, &csize, &pixel) || csize != size) {
        lzxd_free(lzx);
        free((void*)out.data);
        return 0;  // not a shader
      }
      wanted = 1;
    }
  }
  lzxd_free(lzx);
  if (wanted && out.pos == size) {
    size_t csize;
    int pixel;
    if (is_container(out.data, out.pos, &csize, &pixel) && csize == size) {
      int r = write_container(outdir, out.data, size, pixel);
      if (r < 0) { free((void*)out.data); return -1; }
      *written += r;
    }
  }
  free((void*)out.data);
  return 0;
}

static int arc_mode(const char* path, const char* outdir) {
  int fd = open(path, O_RDONLY);
  if (fd < 0) { perror(path); return 1; }
  struct stat st;
  if (fstat(fd, &st) != 0) { perror(path); return 1; }
  size_t len = (size_t)st.st_size;
  const uint8_t* data = (const uint8_t*)mmap(NULL, len, PROT_READ, MAP_PRIVATE, fd, 0);
  if (data == MAP_FAILED) { perror("mmap"); return 1; }

  const char* kind = (const char*)data + 0x1C;
  if (len < 0x24 || (memcmp(kind, "PAM-LZX\0", 8) && memcmp(kind, "PAM-NIL\0", 8))) {
    fprintf(stderr, "%s: not a game archive\n", path);
    return 1;
  }
  size_t data_base = (size_t)be32(data) + 4;
  size_t count = be32(data + 8);
  if (0x24 + count * 12 > len || data_base > len) {
    fprintf(stderr, "%s: entry table outside the file\n", path);
    return 1;
  }
  int written = 0, failed = 0;
  for (size_t i = 0; i < count; ++i) {
    const uint8_t* row = data + 0x24 + i * 12;
    size_t offset = data_base + be32(row + 4);
    size_t length = be32(row + 8);
    if (offset + length > len || length < 12) { ++failed; continue; }
    const uint8_t* entry = data + offset;
    if (memcmp(entry, "PAM-LZX\0", 8) != 0) {
      // Stored (PAM-NIL): the entry is the data.
      size_t csize;
      int pixel;
      if (length <= (1u << 20) && is_container(entry, length, &csize, &pixel) && csize == length) {
        if (write_container(outdir, entry, length, pixel) < 0) return 1;
        ++written;
      }
      continue;
    }
    if (length < 0x2C) { ++failed; continue; }
    uint32_t parts = be32(entry + 8);
    uint32_t header_size = be32(entry + 0x24);
    uint32_t size = be32(entry + 0x10);
    uint32_t window = be32(entry + 0x0C);
    if (parts != 1 || header_size != 0x2C) continue;  // multi-part: no shader is one
    if (size < 12 || size > (1u << 20)) continue;
    if (window < (1u << 15) || window > (1u << 21) || (window & (window - 1))) { ++failed; continue; }
    int window_bits = 0;
    while ((1u << window_bits) < window) ++window_bits;
    int before = written;
    if (arc_entry(entry + 0x2C, length - 0x2C, size, window_bits, outdir, &written) != 0) {
      ++failed;
      fprintf(stderr, "%s: entry %zu (offset %zu) did not decode\n", path, i + 1, offset);
    }
    (void)before;
  }
  munmap((void*)data, len);
  close(fd);
  printf("%s: %zu entries, %d containers, %d failed\n", path, count, written, failed);
  return failed ? 2 : 0;
}

// ---------------------------------------------------------------- xex mode

// Retail key ReXGlue uses (xex_module.cpp); the title key in the security info is AES-decrypted with it.
static const uint8_t kRetailKey[16] = {0x20, 0xB1, 0x85, 0xA5, 0x9D, 0x28, 0xFD, 0xC3,
                                       0x40, 0x58, 0x3F, 0xBB, 0x08, 0x96, 0xBF, 0x91};

static int aes_cbc_decrypt(const uint8_t* key, const uint8_t* iv, const uint8_t* in, size_t len,
                           uint8_t* out) {
  size_t moved = 0;
  CCCryptorStatus status = CCCrypt(kCCDecrypt, kCCAlgorithmAES, 0, key, kCCKeySizeAES128, iv, in,
                                   len, out, len, &moved);
  return status == kCCSuccess && moved == len;
}

static int aes_ecb_decrypt_block(const uint8_t* key, const uint8_t* in, uint8_t* out) {
  size_t moved = 0;
  CCCryptorStatus status = CCCrypt(kCCDecrypt, kCCAlgorithmAES, kCCOptionECBMode, key,
                                   kCCKeySizeAES128, NULL, in, 16, out, 16, &moved);
  return status == kCCSuccess && moved == 16;
}

static int xex_mode(const char* path, const char* out_path) {
  FILE* f = fopen(path, "rb");
  if (!f) { perror(path); return 1; }
  fseek(f, 0, SEEK_END);
  long long file_len = ftell(f);
  fseek(f, 0, SEEK_SET);
  uint8_t* file = (uint8_t*)malloc((size_t)file_len);
  if (!file || fread(file, 1, (size_t)file_len, f) != (size_t)file_len) { perror(path); return 1; }
  fclose(f);
  if (memcmp(file, "XEX2", 4) != 0) { fprintf(stderr, "%s: not XEX2\n", path); return 1; }
  size_t header_size = be32(file + 8);
  size_t security = be32(file + 0x10);
  size_t opt_count = be32(file + 0x14);
  size_t image_size = be32(file + security + 4);

  const uint8_t* fmt = NULL;
  for (size_t i = 0; i < opt_count; ++i) {
    uint32_t key = be32(file + 0x18 + i * 8);
    uint32_t value = be32(file + 0x1C + i * 8);
    if (key == 0x3FF) fmt = file + value;
  }
  if (!fmt) { fprintf(stderr, "%s: no file format info\n", path); return 1; }
  int encryption = be16(fmt + 4);
  int compression = be16(fmt + 6);
  if (encryption > 1) { fprintf(stderr, "%s: encryption type %d\n", path, encryption); return 1; }

  uint8_t session_key[16] = {0};
  if (encryption == 1) {
    if (!aes_ecb_decrypt_block(kRetailKey, file + security + 0x150, session_key)) {
      fprintf(stderr, "%s: title key did not decrypt\n", path);
      return 1;
    }
  }

  const uint8_t* exe = file + header_size;
  size_t exe_len = (size_t)file_len - header_size;
  uint8_t* plain = (uint8_t*)malloc(exe_len + 16);
  if (!plain) return 1;
  memset(plain, 0, exe_len + 16);
  if (encryption == 1) {
    static const uint8_t zero_iv[16] = {0};
    if (!aes_cbc_decrypt(session_key, zero_iv, exe, exe_len & ~(size_t)15, plain)) {
      fprintf(stderr, "%s: image did not decrypt\n", path);
      return 1;
    }
  } else {
    memcpy(plain, exe, exe_len);
  }

  if (compression == 0) {
    FILE* out = fopen(out_path, "wb");
    if (!out) { perror(out_path); return 1; }
    fwrite(plain, 1, exe_len, out);
    fclose(out);
    printf("%s: stored, %zu bytes -> %s\n", path, exe_len, out_path);
    return 0;
  }
  if (compression == 1) {
    // Basic: each block is {u32 data_size, u32 zero_size}; the data is stored (decrypted) as it is.
    size_t block_count = (be32(fmt) - 8) / 8;
    size_t out_size = 0;
    for (size_t i = 0; i < block_count; ++i) out_size += be32(fmt + 8 + i * 8) + be32(fmt + 12 + i * 8);
    uint8_t* image = (uint8_t*)calloc(1, out_size ? out_size : 1);
    size_t at = 0, p = 0;
    for (size_t i = 0; i < block_count; ++i) {
      size_t data_size = be32(fmt + 8 + i * 8);
      size_t zero_size = be32(fmt + 12 + i * 8);
      if (p + data_size > exe_len || at + data_size + zero_size > out_size) {
        fprintf(stderr, "%s: basic block %zu outside the file\n", path, i);
        return 1;
      }
      memcpy(image + at, plain + p, data_size);
      p += data_size;
      at += data_size + zero_size;
    }
    FILE* out = fopen(out_path, "wb");
    if (!out) { perror(out_path); return 1; }
    fwrite(image, 1, out_size, out);
    fclose(out);
    printf("%s: basic, %zu blocks, %zu bytes -> %s\n", path, block_count, out_size, out_path);
    return 0;
  }
  if (compression != 2) { fprintf(stderr, "%s: compression type %d\n", path, compression); return 1; }

  // Normal: per block {u32 size, u8 hash[20], then u16-size chunks until a zero size}, then LZX.
  uint32_t window = be32(fmt + 8);
  size_t deblocked_len = 0, cap = exe_len * 2 + 0x10000;
  uint8_t* deblocked = (uint8_t*)malloc(cap);
  if (!deblocked) return 1;
  size_t p = 0;
  uint32_t block_size = be32(fmt + 12);
  while (block_size) {
    if (p + block_size > exe_len) { fprintf(stderr, "%s: block outside the file\n", path); return 1; }
    size_t next = p + block_size;
    size_t q = p + 24;
    for (;;) {
      if (q + 2 > next) { fprintf(stderr, "%s: block cut short\n", path); return 1; }
      size_t chunk = be16(plain + q);
      q += 2;
      if (!chunk) break;
      if (q + chunk > next || deblocked_len + chunk > cap) {
        fprintf(stderr, "%s: chunk outside the block\n", path);
        return 1;
      }
      memcpy(deblocked + deblocked_len, plain + q, chunk);
      deblocked_len += chunk;
      q += chunk;
    }
    block_size = be32(plain + p);  // the next block's size, at the start of this block
    p = next;
  }

  int window_bits = 0;
  while ((1u << window_bits) < window) ++window_bits;
  MemBuf in = {deblocked, deblocked_len, 0};
  MemBuf out = {(const uint8_t*)calloc(1, image_size), image_size, 0};
  if (!out.data) return 1;
  memset(&g_file, 0, sizeof(g_file));
  g_file.in = &in;
  g_file.out = &out;
  struct lzxd_stream* lzx = lzxd_init(&g_system, (struct mspack_file*)&g_file,
                                      (struct mspack_file*)&g_file, window_bits, 0, 0x8000,
                                      (off_t)image_size, 0);
  if (!lzx) { fprintf(stderr, "%s: LZX init failed\n", path); return 1; }
  int err = lzxd_decompress(lzx, (off_t)image_size);
  lzxd_free(lzx);
  if (err != MSPACK_ERR_OK) { fprintf(stderr, "%s: LZX error %d\n", path, err); return 1; }
  FILE* out_file = fopen(out_path, "wb");
  if (!out_file) { perror(out_path); return 1; }
  fwrite(out.data, 1, image_size, out_file);
  fclose(out_file);
  printf("%s: normal, %zu de-blocked bytes -> %s (%zu bytes, window %u)\n", path, deblocked_len,
         out_path, image_size, window);
  return 0;
}

// ---------------------------------------------------------------- packcheck mode

// The checks src/native/shader_pack.cpp makes on the pack it loads, so a pack this script wrote is
// known to load before it is pushed to a device.
static uint64_t load64le(const uint8_t* p) {
  uint64_t v = 0;
  for (int i = 7; i >= 0; --i) v = (v << 8) | p[i];
  return v;
}
static uint32_t load32le(const uint8_t* p) {
  return (uint32_t)p[0] | (uint32_t)p[1] << 8 | (uint32_t)p[2] << 16 | (uint32_t)p[3] << 24;
}

static int packcheck_mode(const char* path, const char* id_text) {
  FILE* f = fopen(path, "rb");
  if (!f) { perror(path); return 1; }
  fseek(f, 0, SEEK_END);
  long long len = ftell(f);
  fseek(f, 0, SEEK_SET);
  uint8_t* bytes = (uint8_t*)malloc((size_t)len);
  if (!bytes || fread(bytes, 1, (size_t)len, f) != (size_t)len) { perror(path); return 1; }
  fclose(f);
  uint64_t expected_id = strtoull(id_text, NULL, 16);
  const size_t header = 32, entry_size = 24, slot_size = 16;
  int bad = 0;
  if (len < (long long)header || memcmp(bytes, "WWSP", 4) != 0) {
    fprintf(stderr, "not a shader pack\n");
    return 1;
  }
  if (load32le(bytes + 4) != 1) { fprintf(stderr, "format version %u, not 1\n", load32le(bytes + 4)); return 1; }
  uint64_t converter_id = load64le(bytes + 8);
  uint32_t entries = load32le(bytes + 16), slots = load32le(bytes + 20);
  uint32_t compressed = load32le(bytes + 24), spirv_size = load32le(bytes + 28);
  uint64_t described = header + (uint64_t)entries * entry_size + (uint64_t)slots * slot_size + compressed;
  if ((uint64_t)len != described) {
    fprintf(stderr, "%lld bytes, the header describes %llu\n", len, (unsigned long long)described);
    return 1;
  }
  if (converter_id != expected_id) {
    fprintf(stderr, "converted with patch %016llx, expected %016llx\n",
            (unsigned long long)converter_id, (unsigned long long)expected_id);
    ++bad;
  }
  if (!entries) { fprintf(stderr, "the pack holds no shaders\n"); return 1; }
  const uint8_t* entry = bytes + header;
  for (uint32_t i = 0; i < entries; ++i, entry += entry_size) {
    uint64_t hash = load64le(entry);
    uint64_t end = (uint64_t)load32le(entry + 8) + load32le(entry + 12);
    if (end > spirv_size) { fprintf(stderr, "shader %016llx outside the SPIR-V\n", (unsigned long long)hash); ++bad; }
    if (i && hash <= load64le(entry - entry_size)) { fprintf(stderr, "shaders not sorted at %u\n", i); ++bad; }
  }
  const uint8_t* slot = entry;
  for (uint32_t i = 0; i < slots; ++i, slot += slot_size) {
    if (i && load64le(slot) <= load64le(slot - slot_size)) { fprintf(stderr, "slots not sorted at %u\n", i); ++bad; }
  }
  uint8_t* spirv = (uint8_t*)malloc(spirv_size ? spirv_size : 1);
  const uint8_t* src = slot;
  size_t got = ZSTD_decompress(spirv, spirv_size, src, compressed);
  if (ZSTD_isError(got) || got != spirv_size) {
    fprintf(stderr, "the SPIR-V does not decompress (%s)\n", ZSTD_getErrorName(got));
    ++bad;
  } else {
    // Every shader's stream must start with smol-v's magic.
    for (uint32_t i = 0; i < entries; ++i) {
      const uint8_t* e = bytes + header + (size_t)i * entry_size;
      uint32_t at = load32le(e + 8), size = load32le(e + 12);
      if (size < 4 || load32le(spirv + at) != 0x534D4F4Cu) {
        fprintf(stderr, "shader %016llx is not a smol-v stream\n", (unsigned long long)load64le(e));
        ++bad;
        break;
      }
    }
  }
  printf("%s: %u shaders, %u fetch slots, %u bytes SPIR-V (zstd %u), converter %016llx%s\n", path,
         entries, slots, spirv_size, compressed, (unsigned long long)converter_id,
         bad ? " -- PROBLEMS" : " -- ok");
  free(spirv);
  free(bytes);
  return bad ? 1 : 0;
}

int main(int argc, char** argv) {
  if (argc != 4) {
    fprintf(stderr, "usage: lzxtool arc <archive.arc> <outdir>\n"
                    "       lzxtool xex <module.xex> <out.img>\n"
                    "       lzxtool packcheck <pack> <converter id hex>\n");
    return 64;
  }
  if (!strcmp(argv[1], "arc")) return arc_mode(argv[2], argv[3]);
  if (!strcmp(argv[1], "xex")) return xex_mode(argv[2], argv[3]);
  if (!strcmp(argv[1], "packcheck")) return packcheck_mode(argv[2], argv[3]);
  fprintf(stderr, "unknown mode %s\n", argv[1]);
  return 64;
}
LZXTOOL_C
mspack=$sdk/thirdparty/libmspack/libmspack
zstd=$xr_build/thirdparty/zstd
"$clang" -O2 -o "$out/build/lzxtool" "$out/build/lzxtool.c" "$mspack/mspack/lzxd.c" \
    -I"$mspack/mspack" -I"$mspack" -I"$zstd/lib" -I"$xr_src/thirdparty/zstd/lib" \
    "$zstd/build/cmake/lib/libzstd.a" -w \
    || die "could not build the extractor (needs Xcode command line tools and the XenosRecomp build tree at $xr_build)"

cat > "$out/build/module_containers.py" <<'MODULE_CONTAINERS_PY'
"""Write the shader containers in a decompressed Module image to <outdir>.

The game's own Module mode (src/module_shaders.cpp, --ts4_prepare_modules) scans the read-only data
sections of Loader_DLL and Swing_DLL for Xenos shader containers; this does the same on the
decompressed image, with the same container rule (ShaderContainer.cs / module_shaders.h).

usage: module_containers.py <image> <outdir>
"""

import struct
import sys
from pathlib import Path

MAX_CONTAINER = 1 << 20


def sections(image: bytes):
    lfanew = struct.unpack_from("<I", image, 0x3C)[0]
    if image[lfanew : lfanew + 4] != b"PE\0\0":
        raise ValueError("no PE header")
    count = struct.unpack_from("<H", image, lfanew + 6)[0]
    optional = struct.unpack_from("<H", image, lfanew + 20)[0]
    table = lfanew + 24 + optional
    for i in range(count):
        entry = image[table + i * 40 : table + (i + 1) * 40]
        name = entry[:8].rstrip(b"\0").decode("latin-1")
        virtual_size, address = struct.unpack_from("<II", entry, 8)
        characteristics = struct.unpack_from("<I", entry, 36)[0]
        yield name, address, virtual_size, characteristics


def containers(data: bytes):
    at = 0
    while at + 12 <= len(data):
        if data[at] == 0x10 and data[at + 1] == 0x2A and data[at + 2] == 0x11 and data[at + 3] <= 1:
            header, microcode = struct.unpack_from(">II", data, at + 4)
            size = header + microcode
            if 12 <= header and microcode and size <= MAX_CONTAINER and at + size <= len(data):
                yield data[at : at + size], data[at + 3] == 0
                at = (at + size + 3) & ~3
                continue
        at += 4


def fnv1a64(data: bytes) -> int:
    value = 14695981039346656037
    for byte in data:
        value = ((value ^ byte) * 1099511628211) & 0xFFFFFFFFFFFFFFFF
    return value


def main(argv: list[str]) -> int:
    image_path, outdir = Path(argv[0]), Path(argv[1])
    image = image_path.read_bytes()
    outdir.mkdir(parents=True, exist_ok=True)
    found = written = 0
    for name, address, size, characteristics in sections(image):
        # The same sections the game reads: no code, no execute, no write.
        if characteristics & (0x20 | 0x20000000 | 0x80000000):
            continue
        for container, pixel in containers(image[address : address + size]):
            found += 1
            path = outdir / f"{'ps' if pixel else 'vs'}_{fnv1a64(container):016x}.bin"
            if not path.exists():
                path.write_bytes(container)
                written += 1
    print(f"{image_path.name}: {found} containers, {written} new")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
MODULE_CONTAINERS_PY

cat > "$out/build/shader_pack.py" <<'SHADER_PACK_PY'
"""Write the shader pack the native renderer loads, from XenosRecomp's output.

The reference's scripts/shader_pack.py and scripts/shader_fetch_slots.py, together: the entry table
and the zstd frame come from XenosRecomp's shader_cache.cpp, and each shader's texture fetch slots
from the HLSL it wrote. Format: src/native/shader_pack.h (magic "WWSP", version 1).

usage: shader_pack.py <shader_cache.cpp> <hlsl dir> <converter id hex> <output> [<patch dir>]
"""

import hashlib
import re
import struct
import sys
from pathlib import Path

MAGIC = b"WWSP"
VERSION = 1

ENTRY = re.compile(
    r"\{\s*(0x[0-9A-Fa-f]+|\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\}")
ENTRY_TABLE = re.compile(r"g_shaderCacheEntries\[\]\s*=\s*\{(.*?)\n\};", re.DOTALL)
SPIRV = re.compile(r"g_compressedSpirvCache\[\]\s*=\s*\{([^}]*)\}")
SIZE = re.compile(r"\b(g_spirvCacheCompressedSize|g_spirvCacheDecompressedSize)\s*=\s*(\d+)")

SLOTS = 32
ALL_SLOTS = (1 << SLOTS) - 1
DEFINE = re.compile(
    r"^#define (\w+)_(?:Texture\w+|Sampler)DescriptorIndex "
    r"vk::RawBufferLoad<uint>\(g_PushConstants\.SharedConstants \+ (\d+)\)",
    re.MULTILINE,
)
REFERENCE = re.compile(r"\b(\w+)_(?:Texture(?:2D|3D|Cube)|Sampler)DescriptorIndex\b")
HLSL_NAME = re.compile(r"^(?:ps|vs)_([0-9a-f]{16})\.hlsl$")


def slot_mask(hlsl: str) -> int:
    slots = {name: (int(offset) // 4) % SLOTS for name, offset in DEFINE.findall(hlsl)}
    mask = 0
    for line in hlsl.splitlines():
        stripped = line.strip()
        if stripped.startswith("#define") or "packoffset" in stripped:
            continue
        for name in REFERENCE.findall(line):
            if name not in slots:
                return ALL_SLOTS
            mask |= 1 << slots[name]
    return mask


def fetch_slots(hlsl_dir: Path) -> list[tuple[int, int]]:
    entries = []
    for path in sorted(hlsl_dir.glob("*.hlsl")):
        match = HLSL_NAME.match(path.name)
        if match:
            entries.append((int(match.group(1), 16), slot_mask(path.read_text(errors="replace"))))
    return sorted(entries)


def converter_id(patch_dir: Path) -> int:
    """The reference CMakeLists.txt's id: SHA-256 of the patch series' hex, first 16 hex digits."""
    series = "".join(path.read_bytes().hex() for path in sorted(patch_dir.glob("*.patch")))
    if not series:
        raise SystemExit(f"no patches in {patch_dir}")
    return int(hashlib.sha256(series.encode("ascii")).hexdigest()[:16], 16)


def main(argv: list[str]) -> int:
    cache_path, hlsl_dir, output = Path(argv[0]), Path(argv[1]), Path(argv[3])
    patch_dir = Path(argv[4]) if len(argv) > 4 else None
    text = cache_path.read_text(encoding="utf-8", errors="replace")
    table, spirv, sizes = ENTRY_TABLE.search(text), SPIRV.search(text), dict(SIZE.findall(text))
    if not table or not spirv or len(sizes) != 2:
        raise SystemExit("not a XenosRecomp shader_cache.cpp")
    entries = sorted(
        (int(h, 0), int(offset), int(size), int(mask))
        for h, _, _, offset, size, mask in ENTRY.findall(table.group(1))
    )
    compressed = bytes(int(b) for b in spirv.group(1).replace("\n", "").split(",") if b.strip())
    if len(compressed) != int(sizes["g_spirvCacheCompressedSize"]):
        raise SystemExit("compressed SPIR-V does not match its size")
    spirv_size = int(sizes["g_spirvCacheDecompressedSize"])
    # The reference computes the id from the patch files; the script passes it in when it has them.
    identifier = int(argv[2], 16) if argv[2] != "-" else converter_id(patch_dir)
    slots = fetch_slots(hlsl_dir)

    out = bytearray(MAGIC)
    out += struct.pack("<IQIIII", VERSION, identifier, len(entries), len(slots),
                       len(compressed), spirv_size)
    for shader_hash, offset, size, mask in entries:
        out += struct.pack("<QIIII", shader_hash, offset, size, mask, 0)
    for fnv, mask in slots:
        out += struct.pack("<QII", fnv, mask, 0)
    out += compressed
    temporary = output.with_name(output.name + ".tmp")
    temporary.write_bytes(out)
    temporary.replace(output)
    unsure = sum(1 for _, mask in slots if mask == ALL_SLOTS)
    print(f"{len(entries)} shaders, {len(slots)} fetch slots ({unsure} bind every slot), "
          f"{len(out)} bytes, converter {identifier:016x}: {output}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
SHADER_PACK_PY

cat > "$out/build/screen_one.sh" <<'SCREEN_ONE_SH'
#!/bin/sh
# Convert one container alone; print its path when XenosRecomp cannot. $1 binary, $2 include, $3 file.
d=$(mktemp -d)
mkdir -p "$d/in"
cp "$3" "$d/in/"
if ! "$1" "$d/in" "$d/shader_cache.cpp" "$2" "$d/hlsl" >/dev/null 2>&1; then echo "$3"; fi
rm -rf "$d"
SCREEN_ONE_SH
chmod +x "$out/build/screen_one.sh"

# ---------------------------------------------------------------------------- 2. containers

step "reading shader containers from the game data"
containers=$out/containers
mkdir -p "$containers"
: > "$logs/shader-arcs.log"
archives=0
while IFS= read -r archive; do
    archives=$((archives + 1))
    "$out/build/lzxtool" arc "$archive" "$containers" >> "$logs/shader-arcs.log" 2>&1 \
        || printf 'FAILED %s\n' "$archive" >> "$logs/shader-arcs.log"
done < <(find "$gamedata" -iname '*.arc' -type f | LC_ALL=C sort)
failures=$(grep -c 'FAILED\|did not decode' "$logs/shader-arcs.log" || true)
[ "$failures" = 0 ] || die "$failures archives or entries did not decode; see $logs/shader-arcs.log"
echo "$archives archives, $(count "$containers") containers so far"

# The Modules' own shaders (no archive holds them): decompress the XEX images, then scan the
# read-only sections for containers, as src/module_shaders.cpp does in memory.
mkdir -p "$out/xex"
for module in Loader_DLL Swing_DLL; do
    [ -f "$gamedata/$module.xex" ] || die "no $module.xex in $gamedata"
    "$out/build/lzxtool" xex "$gamedata/$module.xex" "$out/xex/$module.img" \
        >> "$logs/shader-arcs.log" 2>&1 || die "could not decompress $module.xex"
    "$python" -I "$out/build/module_containers.py" "$out/xex/$module.img" "$containers"
done

# Shaders the device reported missing. The native renderer dumps the raw container through
# ShaderTopUp::Offer whenever it asks for a shader the pack lacks (src/native/shader_topup.cpp);
# Android cannot run the converter live, so those dumps are pulled with
#   adb pull /sdcard/.../files/cache/shader-topup/. work/shaderpack/device-topup/
# and converted here like any other container. This is also how TU1-only variants get in, since
# the device runs the patched modules (see docs/shaders.md, "Gaps and caveats").
if [ -d "$out/device-topup" ]; then
    dumped=$(count "$out/device-topup")
    if [ "$dumped" -gt 0 ]; then
        echo "device top-up: $dumped container(s) from a device run"
        cp -n "$out/device-topup"/*.bin "$containers"/ 2>/dev/null || true
    fi
fi

total=$(count "$containers")
echo "containers: $total"

# ---------------------------------------------------------------------------- 3. convert

converted=$out/converted
if [ "$convert" = 1 ]; then
    step "converting $total containers with XenosRecomp"
    binary=$xr_build/XenosRecomp/XenosRecomp
    if [ ! -x "$binary" ]; then
        echo "no XenosRecomp binary at $binary: building it"
        "$cmake" -S "$xr_src" -B "$xr_build" -G Ninja -DCMAKE_BUILD_TYPE=Release \
            -DCMAKE_OSX_ARCHITECTURES=arm64 -DCMAKE_C_COMPILER="$clang" \
            -DCMAKE_CXX_COMPILER="${CXX:-/usr/bin/clang++}" -DCMAKE_CXX_FLAGS=-DSVR2011_RECOMP \
            -DXENOS_RECOMP_DXIL=OFF > "$logs/xenosrecomp-configure.log" 2>&1 \
            || die "XenosRecomp configure failed; see $logs/xenosrecomp-configure.log"
        "$cmake" --build "$xr_build" --parallel "$(sysctl -n hw.ncpu)" \
            > "$logs/xenosrecomp-build.log" 2>&1 \
            || die "XenosRecomp build failed; see $logs/xenosrecomp-build.log"
    fi
    include=$xr_src/XenosRecomp/shader_common.h
    # libdxcompiler.dylib sits in the submodule; the binary finds it through DYLD_LIBRARY_PATH.
    export DYLD_LIBRARY_PATH=$xr_src/thirdparty/dxc-bin/lib/arm64${DYLD_LIBRARY_PATH:+:$DYLD_LIBRARY_PATH}
    rm -rf "$converted"
    mkdir -p "$converted"
    convert_all() {
        "$binary" "$containers" "$converted/shader_cache.cpp" "$include" "$converted/hlsl" \
            > "$logs/shader-convert.log" 2>&1
    }
    if ! convert_all; then
        # XenosRecomp stops on the first shader it cannot convert and writes no cache. Try each
        # container alone (the Launcher's screening), set aside the ones that fail, and convert the
        # rest together.
        echo "the whole set failed: screening each container"
        mkdir -p "$out/skipped"
        find "$containers" -name '*.bin' -print0 | xargs -0 -n 1 -P "$(sysctl -n hw.ncpu)" \
            "$out/build/screen_one.sh" "$binary" "$include" > "$out/skipped.txt" || true
        if [ -s "$out/skipped.txt" ]; then
            while IFS= read -r bad; do mv "$bad" "$out/skipped/"; done < "$out/skipped.txt"
            echo "$(wc -l < "$out/skipped.txt" | tr -d ' ') containers set aside in $out/skipped"
        fi
        rm -rf "$converted"
        mkdir -p "$converted"
        convert_all || die "XenosRecomp failed again after screening; see $logs/shader-convert.log"
    fi
    echo "converted: $(find "$converted/hlsl" -name '*.hlsl' | wc -l | tr -d ' ') shaders"
fi
[ -f "$converted/shader_cache.cpp" ] || die "no converted shaders at $converted (drop --no-convert)"

# ---------------------------------------------------------------------------- 4. the pack

step "writing the pack"
# The id the app build computes from the same patch series (ts4-port/CMakeLists.txt).
id=$("$python" -I - "$patches" <<'PY'
import hashlib, pathlib, sys
series = "".join(p.read_bytes().hex() for p in sorted(pathlib.Path(sys.argv[1]).glob("*.patch")))
print(hashlib.sha256(series.encode("ascii")).hexdigest()[:16])
PY
)
"$python" -I "$out/build/shader_pack.py" "$converted/shader_cache.cpp" "$converted/hlsl" "$id" \
    "$out/topspin4.shaderpack"

step "checking the pack"
"$out/build/lzxtool" packcheck "$out/topspin4.shaderpack" "$id" \
    || die "the pack does not load; see above"

printf '\n%s\n' "$out/topspin4.shaderpack ($(du -h "$out/topspin4.shaderpack" | cut -f1))"
echo "push it to the app's cache folder as topspin4.shaderpack (the runtime's cache_root)."
