#ifndef BASETYPES_H
#define BASETYPES_H

typedef signed char s8;
typedef unsigned char   u8;
typedef short  s16;
typedef unsigned short  u16;
typedef long  s32;
typedef unsigned long   u32;
typedef long long  s64;
typedef unsigned long long   u64;

typedef u8* ptu8;
typedef u16* ptu16;
typedef u32* ptu32;
typedef u64* ptu64;

typedef const s8* cpts8;
typedef const u8* cptu8;

#define BASETYPE_S8_MIN  (-128)                         /* minimum signed 8-bit value */
#define BASETYPE_S16_MIN (-32768)                       /* minimum signed 16-bit value */
#define BASETYPE_S32_MIN (-2147483647 - 1)              /* minimum signed 32-bit value */
#define BASETYPE_S64_MIN (-9223372036854775807LL - 1)   /* minimum signed 64-bit value */

#define BASETYPE_S8_MAX 127                             /* maximum signed 8-bit value */
#define BASETYPE_S16_MAX 32767                          /* maximum signed 16-bit value */
#define BASETYPE_S32_MAX 2147483647                     /* maximum signed 32-bit value */
#define BASETYPE_S64_MAX 9223372036854775807LL          /* maximum signed 64-bit value */

#define BASETYPE_U8_MAX 255U                            /* maximum unsigned 8-bit value */
#define BASETYPE_U16_MAX 65535U                         /* maximum unsigned 16-bit value */
#define BASETYPE_U32_MAX 4294967295U                    /* maximum unsigned 32-bit value */
#define BASETYPE_U64_MAX 18446744073709551615ULL        /* maximum unsigned 64-bit value */

#define INT8_MAX BASETYPE_S8_MAX                        /* signed 8-bit max */
#define INT16_MAX BASETYPE_S16_MAX                      /* signed 16-bit max */
#define INT32_MAX BASETYPE_S32_MAX                      /* signed 32-bit max */
#define INT64_MAX BASETYPE_S64_MAX                      /* signed 64-bit max */

#define UINT8_MAX BASETYPE_U8_MAX                       /* unsigned 8-bit max */
#define UINT16_MAX BASETYPE_U16_MAX                     /* unsigned 16-bit max */
#define UINT32_MAX BASETYPE_U32_MAX                     /* unsigned 32-bit max */
#define UINT64_MAX BASETYPE_U64_MAX                     /* unsigned 64-bit max */


#endif
