//-*-c++-*-
#ifndef __IRRCOMPILECONFIG_H__
#define __IRRCOMPILECONFIG_H__

// If you want to changed the default behavior of IrrLicht, 
// . In the pre build process of your game project, 
// . call "$(IrrLichtPath)\tools\setupCompileConfig.bat $(your_config_file.h)"
// . This command will overwrite "$(IrrLichtPath)\include\IrrCompileGameConfig.h" that should not be versionned
// Otherwise, IrrLicht will copy "$(IrrLichtPath)\include\IrrCompileDefaultConfig.h"
// to "$(IrrLichtPath)\include\IrrCompileGameConfig.h"

// Runtime textures use the standard image loaders (PNG, TGA and BMP).
#define _IRR_ENABLE_PROCESS_BUFFER_HEAP_EXCESS_
#define _IRR_COMPILE_WITH_COLLADA_PARTICLE_SYSTEM_

#define _IRR_KEEP_DEPRECATED_

#include "IrrCompileGameConfig.h"

#ifndef _IRR_THROW
#define _IRR_THROW(x) throw x
#endif

#ifdef __BIG_ENDIAN__
#    define _IRR_BIG_ENDIAN_
#endif

#if defined(_IRR_XBOX_PLATFORM_) \
	|| defined(_IRR_COMPILE_WITH_DIRECT3D_8_) \
	|| defined(_IRR_COMPILE_WITH_DIRECT3D_9_)
#    define _IRR_SCOLOR_USE_ARGB_
#else
#    define _IRR_SCOLOR_USE_RGBA_
#endif

#if !defined(_IRR_USE_WII_DEVICE_) && !defined(_IRR_USE_IPHONEOS_DEVICE_)
#    define _IRR_HAS_STENCIL_BUFFER_
#endif

#ifdef _IRR_COMPILE_WITH_EDITOR_
#    define IRR_DEBUG_VIRTUAL virtual
#else
#    define IRR_DEBUG_VIRTUAL
#endif

#ifndef _IRR_PROCESS_BUFFER_HEAP_SIZE_
#    define _IRR_PROCESS_BUFFER_HEAP_SIZE_ (64 * 1024)
#endif

#ifndef _IRR_DEFAULT_COMPILED_BATCH_MAX_VERTEX_BUFFER_SIZE_
#    define _IRR_DEFAULT_COMPILED_BATCH_MAX_VERTEX_BUFFER_SIZE_ 5242880
#endif

#ifndef _IRR_DEFAULT_COMPILED_BATCH_MAX_INDEX_BUFFER_SIZE_
#    define _IRR_DEFAULT_COMPILED_BATCH_MAX_INDEX_BUFFER_SIZE_ \
	(_IRR_DEFAULT_COMPILED_BATCH_MAX_VERTEX_BUFFER_SIZE_ / 8)
#endif

#ifndef _IRR_DEFAULT_DYNAMIC_BATCH_MAX_VERTEX_BUFFER_SIZE_
#    define _IRR_DEFAULT_DYNAMIC_BATCH_MAX_VERTEX_BUFFER_SIZE_ \
	(_IRR_PROCESS_BUFFER_HEAP_SIZE_ / 2)
#endif

#ifndef _IRR_DEFAULT_DYNAMIC_BATCH_MAX_INDEX_BUFFER_SIZE_
#    define _IRR_DEFAULT_DYNAMIC_BATCH_MAX_INDEX_BUFFER_SIZE_ \
	(_IRR_DEFAULT_DYNAMIC_BATCH_MAX_VERTEX_BUFFER_SIZE_ / 8)
#endif

#ifndef _IRR_MAX_DYNAMIC_BATCH_SEGMENT_SIZE_
#    define _IRR_MAX_DYNAMIC_BATCH_SEGMENT_SIZE_ 16
#endif 

#endif
