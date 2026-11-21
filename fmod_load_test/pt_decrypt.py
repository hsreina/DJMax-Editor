# -*- coding: utf-8 -*-
# Port of DJMaxEditor Files/pt/PtCodec.cs (pt_tool pt_dec.c) for offline inspection.
import struct, sys

M32 = 0xFFFFFFFF
MatrixA = 0x9908B0DF

def build_crc32_table():
    t = []
    for n in range(256):
        c = n
        for _ in range(8):
            c = (0xEDB88320 ^ (c >> 1)) & M32 if (c & 1) else (c >> 1)
        t.append(c)
    assert t[1] == 0x77073096
    return t

CRC32 = build_crc32_table()

def u32(b, off):
    return b[off] | (b[off+1] << 8) | (b[off+2] << 16) | (b[off+3] << 24)

def w32(b, off, v):
    b[off] = v & 0xFF
    b[off+1] = (v >> 8) & 0xFF
    b[off+2] = (v >> 16) & 0xFF
    b[off+3] = (v >> 24) & 0xFF

def get_param1(header):  # CRC32
    v4 = 0xFFFFFFFF
    for i in range(len(header)):
        add = (v4 & 0xFF) ^ header[i]
        v4 = CRC32[add] ^ (v4 >> 8)
    return (~v4) & M32

def get_param2(header):  # byte sum
    return sum(header) & M32

class PtCodec:
    def __init__(self, data):
        self.out = bytearray(data)
        self.header = bytearray(data[:24])
        self.body = bytearray(data[24:])
        self.blob = [0]*626
        self.kdKey1 = bytearray(8)
        self.kdUnknown1 = bytearray(8)
        self.tempKey1 = bytearray(8)
        self.tempKey2 = bytearray(8)
        self.key1 = [0, 0]
        self.kdKey2 = [0, 0]

    def fill_key_data_blob(self):
        self.blob[0] = 0x12BD6AA
        for idx in range(1, 624):
            self.blob[idx] = (idx + 1812433253 * (self.blob[idx-1] ^ (self.blob[idx-1] >> 30))) & M32
        self.blob[624] = 624

    def fill_data(self):
        self.fill_key_data_blob()
        v9, v6 = 1, 0
        a3 = 6
        for _ in range(624, 0, -1):
            kw = u32(self.header, 4*v6)
            self.blob[v9] = (v6 + kw + (self.blob[v9] ^ ((1664525 * (self.blob[v9-1] ^ (self.blob[v9-1] >> 30))) & M32))) & M32
            v9 += 1; v6 += 1
            if v9 >= 624:
                self.blob[0] = self.blob[623]; v9 = 1
            if v6 >= a3: v6 = 0
        for _ in range(623, 0, -1):
            self.blob[v9] = ((self.blob[v9] ^ ((1566083941 * (self.blob[v9-1] ^ (self.blob[v9-1] >> 30))) & M32)) - v9) & M32
            v9 += 1
            if v9 >= 624:
                self.blob[0] = self.blob[623]; v9 = 1
        self.blob[0] = 0x80000000

    def calc_param2(self):
        if self.blob[624] >= 624:
            i = 0
            while i < 227:
                v1 = (self.blob[i+1] & 0x7FFFFFFF) | (self.blob[i] & 0x80000000)
                self.blob[i] = ((MatrixA if (v1 & 1) else 0) ^ self.blob[i+397] ^ (v1 >> 1)) & M32
                i += 1
            while i < 623:
                v2 = (self.blob[i+1] & 0x7FFFFFFF) | (self.blob[i] & 0x80000000)
                self.blob[i] = ((MatrixA if (v2 & 1) else 0) ^ self.blob[i-227] ^ (v2 >> 1)) & M32
                i += 1
            v3 = (self.blob[0] & 0x7FFFFFFF) | (self.blob[623] & 0x80000000)
            self.blob[623] = ((MatrixA if (v3 & 1) else 0) ^ self.blob[396] ^ (v3 >> 1)) & M32
            self.blob[624] = 0
        y = self.blob[self.blob[624]]
        self.blob[624] += 1
        y ^= y >> 11
        y ^= (y << 7) & 0x9D2C5680
        y ^= (y << 15) & 0xEFC60000
        y ^= y >> 18
        return y & M32

    def update_param(self):
        v8, v5 = self.key1[0], self.key1[1]
        s = 0
        k0, k1 = u32(self.kdKey1, 0), u32(self.kdKey1, 4)
        u0, u1 = u32(self.kdUnknown1, 0), u32(self.kdUnknown1, 4)
        for _ in range(32):
            s = (s - 1640531527) & M32
            v8 = (v8 + ((k1 + (v5 >> 5)) & M32 ^ (s + v5) & M32 ^ (k0 + ((16 * v5) & M32)) & M32)) & M32
            v5 = (v5 + ((u1 + (v8 >> 5)) & M32 ^ (s + v8) & M32 ^ (u0 + ((16 * v8) & M32)) & M32)) & M32
        self.key1[0], self.key1[1] = v8, v5
        w32(self.kdKey1, 0, v8); w32(self.kdKey1, 4, v5)
        w32(self.tempKey1, 0, v8); w32(self.tempKey1, 4, v5)

    def run(self):
        dec_flag = u32(self.body, 0)
        mode_decode = not (dec_flag <= 10)
        self.fill_data()
        w32(self.kdKey1, 4, get_param1(self.header))
        w32(self.kdKey1, 0, get_param2(self.header))
        self.key1[0] = u32(self.kdKey1, 0)
        self.key1[1] = u32(self.kdKey1, 4)
        w32(self.tempKey1, 0, self.key1[0]); w32(self.tempKey1, 4, self.key1[1])
        self.kdKey2[0] = self.calc_param2()
        self.kdKey2[1] = self.calc_param2()
        w32(self.tempKey2, 0, self.kdKey2[0]); w32(self.tempKey2, 4, self.kdKey2[1])

        y = 0
        for x in range(len(self.body)):
            if not mode_decode:  # encode: feed pre-XOR plaintext
                self.kdKey1[y] = self.body[x]
                self.kdUnknown1[y] = self.body[x]
            self.body[x] ^= (self.tempKey2[y] ^ self.tempKey1[y]) & 0xFF
            if mode_decode:      # decode: feed post-XOR plaintext
                self.kdKey1[y] = self.body[x]
                self.kdUnknown1[y] = self.body[x]
            y += 1
            if y == 8:
                self.kdUnknown1[:] = self.kdKey1[:]
                self.update_param()
                self.kdKey2[0] = self.calc_param2()
                self.kdKey2[1] = self.calc_param2()
                w32(self.tempKey2, 0, self.kdKey2[0]); w32(self.tempKey2, 4, self.kdKey2[1])
                y = 0

        self.out[:24] = self.header
        self.out[24:] = self.body
        return bytes(self.out), mode_decode

def parse_instruments(data):
    assert data[:4] == b'PTFF'
    version = data[4]
    ins_cnt = struct.unpack_from('<H', data, 0x16)[0]
    off = 0x18
    entries = []
    for _ in range(ins_cnt):
        if version == 1:
            insNo, u1, u2 = struct.unpack_from('<HBB', data, off)
            raw = data[off+4:off+0x44]
            off += 0x44
        else:
            insNo, u1 = struct.unpack_from('<BB', data, off)
            raw = data[off+3:off+0x43]
            off += 0x43
        idx = raw.find(b'\x00')
        name = raw if idx < 0 else raw[:idx]
        tail = raw if idx < 0 else raw[idx+1:]
        entries.append((insNo, u1, u2 if version == 1 else 0, name, any(b != 0 for b in tail)))
    return version, entries

if __name__ == '__main__':
    for fn in sys.argv[1:]:
        data = open(fn, 'rb').read()
        dec, was_enc = PtCodec(data).run()
        tag = 'encrypted->decrypted' if was_enc else 'already-decrypted?!'
        print(f"=== {fn} ({tag}) ===")
        try:
            version, entries = parse_instruments(dec)
            print(f"  version={version} insCnt={len(entries)}")
            for insNo, u1, u2, name, tail_bad in entries:
                flag = '  <<< TAIL NONZERO' if tail_bad else ''
                print(f"  {insNo}: {name.decode('ascii', 'replace')}{flag}")
        except Exception as e:
            print(f"  parse error: {e}")
            open(fn + '.dec', 'wb').write(dec)
