
#include "stdafx.h"
#include <cstdint>
#include "Core/irrlicht.h"

#include "Sprites/ASprite.h"
#include "Sprites/SpriteYaml.h"

////////////////////////////////////////////////////////////////////////////////////////////////////

#define SPRITE_DIR_PATH			"Sprites/"

////////////////////////////////////////////////////////////////////////////////////////////////////

#if DEBUG_SYSTEM

array<std::uintptr_t>		g_SpriteList;

void SpriteList_Add( std::uintptr_t ptr )
{
	g_SpriteList.push_back( ptr );
}

void SpriteList_Remove( std::uintptr_t ptr )
{	
	s32 idx = g_SpriteList.binary_search( ptr );
	if( idx != -1 )
		g_SpriteList.erase( idx );
}

void SpriteList_Dump()
{
	s32 size = g_SpriteList.size();

	DBG_OUT( "Sprites list: %d\n", size );
	//for( s32 i=0; i<size; i++ )
	//{
	//	DBG_OUT( "\t%x\n", g_SpriteList[i] );
	//}
}

#endif

////////////////////////////////////////////////////////////////////////////////////////////////////

s32	ASprite::_index1 = -1;
s32	ASprite::_index2 = -1;
s32 ASprite::_text_w = 0;
s32 ASprite::_text_h = 0;
s32 ASprite::lines = 0;
s32 ASprite::off[MAX_LINES_PER_PAGE];

////////////////////////////////////////////////////////////////////////////////////////////////////

ASprite::ASprite()
{
	_nModules = 0;
	_nFrames = 0;
	_nAnims = 0;
	_nAFrames = 0;
	_modules_x = NULL;
	_modules_y = NULL;
	_modules_w = NULL;
	_modules_h = NULL;

	_frames_nfm = NULL;
	_frames_fm_start = NULL;
	_frames_rc = NULL;
	_frames_col = NULL;
	_frames_frect_start = NULL;
	_frames_frect_num = NULL;

	nFModules = 0;
	_nFModules = NULL;
	_frame_rects = NULL;
	_anims_naf = NULL;
	_anims_af_start = NULL;
	_aframes = NULL;
	bs_flags = 0;
	ex_bs_flags = 0;
	_modules_image = NULL;
	_char_spacing = 0;
	_line_spacing = 4;

	_map_char = NULL;

	_material = NULL;	
	_color = SColor(0xFFFFFFFF);

	indices[0] = 0;
	indices[1] = 1;
	indices[2] = 2;
	indices[3] = 0;
	indices[4] = 2;
	indices[5] = 3;

	vertices[0].Color = video::SColor(0xffffffff);
	vertices[1].Color = video::SColor(0xffffffff);
	vertices[2].Color = video::SColor(0xffffffff);
	vertices[3].Color = video::SColor(0xffffffff);

	_fmodules_correction = 0.0f;

#if DEBUG_SYSTEM
	
	SpriteList_Add( reinterpret_cast<std::uintptr_t>(this) );

#endif
}

ASprite::~ASprite()
{
	_nModules = 0;
	SAFE_DEL_ARRAY( _modules_x );
	SAFE_DEL_ARRAY( _modules_y );
	SAFE_DEL_ARRAY( _modules_w );
	SAFE_DEL_ARRAY( _modules_h );

	SAFE_DEL_ARRAY( _frames_nfm );
	SAFE_DEL_ARRAY( _frames_fm_start );
	SAFE_DEL_ARRAY( _frames_rc );
	SAFE_DEL_ARRAY( _frames_col );
	SAFE_DEL_ARRAY( _frames_frect_start );
	SAFE_DEL_ARRAY( _frames_frect_num );

	nFModules = 0;
	SAFE_DEL_ARRAY( _nFModules );
	SAFE_DEL_ARRAY( _frame_rects );
	SAFE_DEL_ARRAY( _anims_naf );
	SAFE_DEL_ARRAY( _anims_af_start );
	SAFE_DEL_ARRAY( _aframes );
	
	SAFE_DEL( _material );

	if( _modules_image != NULL )
	{
		_modules_image->drop();

		if (_modules_image->getReferenceCount() <= 1)
		{
			__IRR_DRIVER()->setMaterial(SMaterial());
			__IRR_DRIVER()->removeTexture( _modules_image );
		}

		_modules_image = NULL;
	}
	//SAFE_DEL(_vertices);

#if DEBUG_SYSTEM
	
	SpriteList_Remove( reinterpret_cast<std::uintptr_t>(this) );

#endif
}

////////////////////////////////////////////////////////////////////////////////////////////////////

static stringc FindSpriteTexture(stringc image)
{
	image.replace('\\', '/');
	const s32 dot = image.findLast('.');
	const s32 slash = image.findLast('/');
	const stringc base = dot > slash ? image.subString(0, dot) : image;
	for (const char* extension : {".png", ".tga", ".bmp"})
	{
		const stringc candidate = base + extension;
		if (__IRR_FILE_SYSTEM()->existFile(candidate.c_str())) return candidate;
	}
	return __IRR_FILE_SYSTEM()->existFile(image.c_str()) ? image : stringc("");
}

static stringc SpriteImagePath(const char* spriteName, const std::string& image)
{
	stringc path(image.c_str());
	path.replace('\\', '/');
	if (path.size() && (path[0] == '/' || (path.size() > 1 && path[1] == ':'))) return path;
	return __IRR_FILE_SYSTEM()->getFileDir(spriteName) + "/" + path;
}

bool ASprite::Load( const char *name )
{
	if( name == NULL )
		return false;

	stringc file_base = SPRITE_DIR_PATH;
	file_base += name;
	if (file_base.size() >= 5 && file_base.subString(file_base.size() - 5, 5) == ".yaml")
		return LoadYamlSprite(file_base.c_str());
	if (file_base.size() >= 4 && file_base.subString(file_base.size() - 4, 4) == ".yml")
		return LoadYamlSprite(file_base.c_str());
	// Call sites may supply a stem or an explicit YAML filename.
	for (const char* extension : {".yaml", ".yml"})
	{
		const stringc yamlName = file_base + extension;
		if (__IRR_FILE_SYSTEM()->existFile(yamlName.c_str())) return LoadYamlSprite(yamlName.c_str());
	}
	Logs::error("Missing sprite YAML: %s.yaml\n", file_base.c_str());
	return false;
}

bool ASprite::LoadYamlSprite(const char* spriteName)
{
	IReadFile* file = __IRR_FILE_SYSTEM()->createAndOpenFile(spriteName);
	if (!file) return false;
	const long size = file->getSize();
	if (size <= 0 || size > 16 * 1024 * 1024) { file->drop(); return false; }
	std::string source(size, '\0');
	const bool read = file->read(&source[0], size) == size;
	file->drop();
	if (!read) return false;
	SpriteYamlData data;
	std::string error;
	if (!CompileSpriteYaml(source, data, error))
	{
		Logs::error("Invalid sprite YAML %s: %s\n", spriteName, error.c_str());
		return false;
	}
	const stringc texture = FindSpriteTexture(SpriteImagePath(spriteName, data.image));
	if (!texture.size())
	{
		Logs::error("Missing image for sprite YAML %s: %s\n", spriteName, data.image.c_str());
		return false;
	}
	const bool authoringAlpha = data.hasTransparent || !data.alphaImage.empty();
	const stringc textureName = authoringAlpha ? stringc(spriteName) + "#yaml-atlas" : texture;
	if (authoringAlpha && !__IRR_DRIVER()->findTexture(textureName.c_str()))
	{
		// Apply authoring transparency to a separate texture, preserving shared atlases.
		IVideoDriver* driver = __IRR_DRIVER();
		IImage* image = driver->createImageFromFile(texture.c_str());
		IImage* alpha = NULL;
		if (!data.alphaImage.empty())
		{
			const stringc mask = FindSpriteTexture(SpriteImagePath(spriteName, data.alphaImage));
			if (mask.size()) alpha = driver->createImageFromFile(mask.c_str());
		}
		if (!image || (!data.alphaImage.empty() && (!alpha || alpha->getDimension() != image->getDimension())))
		{
			if (image) image->drop();
			if (alpha) alpha->drop();
			Logs::error("Could not load sprite YAML image/alpha mask: %s\n", spriteName);
			return false;
		}
		IImage* rgba = driver->createImage(ECF_A8R8G8B8, image->getDimension());
		if (!rgba) { image->drop(); if (alpha) alpha->drop(); return false; }
		for (s32 y = 0; y < image->getDimension().Height; ++y)
			for (s32 x = 0; x < image->getDimension().Width; ++x)
			{
				SColor color = image->getPixel(x, y);
				if (alpha) color.A = alpha->getPixel(x, y).R;
				const u32 rgb = (color.R << 16) | (color.G << 8) | color.B;
				if (data.hasTransparent && rgb == (data.transparent & 0xffffff)) color.A = 0;
				rgba->setPixel(x, y, color);
			}
		ITexture* prepared = driver->addTexture(textureName.c_str(), rgba);
		rgba->drop(); image->drop(); if (alpha) alpha->drop();
		if (!prepared) return false;
	}
	return Load(data.binary.data()) && LoadTexture(textureName.c_str());
}

bool ASprite::LoadTexture(const char * textureName)
{
	if( _modules_image != NULL )
	{
		_modules_image->drop();

		if (_modules_image->getReferenceCount() <= 1)
		{
			__IRR_DRIVER()->setMaterial(SMaterial());
			__IRR_DRIVER()->removeTexture( _modules_image );
		}

		_modules_image = NULL;
	}

	ITexture* spriteTexture = __IRR_DRIVER()->getTexture(textureName);

	if( spriteTexture == NULL )
		return false;

	spriteTexture->grab();
	
	_modules_image = spriteTexture;
	
	if (!_material)
	{
		_material = NEW video::SMaterial();
		_material->setFlag(EMF_LIGHTING, false);
		_material->setMaterialType(EMT_TRANSPARENT_ALPHA_CHANNEL);
	}
	_material->setTexture(0, _modules_image);

	_material->getTexture(0)->setMinFilter(ETFT_NEAREST);
	_material->getTexture(0)->setMagFilter(ETFT_NEAREST);

	return true;
}

#define ReadByteFromRawData()	(rawdata[index] & 0xFF); index++
#define ReadShortFromRawData()	((rawdata[index] & 0xFF) | (rawdata[index+1] << 8)); index+=2
#define ReadIntFromRawData()	((rawdata[index] & 0xFF) | ((rawdata[index+1] & 0xFF) << 8) | ((rawdata[index+2] & 0xFF) << 16) | ((rawdata[index+3] & 0xFF) << 24)); index+=4
#define ReadFromRawData(dest, len)		memcpy(dest, (rawdata+index), len); index+=len

bool ASprite::Load( byte* rawdata )
{
	s32 index = 0;
 	//TRACE("AURORA: File size = " + file.length + "; Offset = " + offset);
	short bs_version = ReadShortFromRawData();

 	//TRACE("AURORA: BSprite version = 0x" + Integer.toHexString(bs_version));

	if (bs_version != BSPRITE_v003 && bs_version != BSPRITE_v004 && bs_version != BSPRITE_v005) 
	{
 		//if (AURORA_DEBUG) TRACE("AURORA: ERROR - Invalid BSprite version!");
		return false;
	}

	bs_flags = ReadIntFromRawData();

	if (bs_version == BSPRITE_v004)
	{
		ex_bs_flags = ReadIntFromRawData();
	}
	s32 MaxRectUsed = 0;

	// Modules
	if ((bs_flags & BS_MODULES) != 0) 
	{
		_nModules = ReadShortFromRawData();

 		//if (AURORA_DEBUG) TRACE("AURORA: nModules = " + _nModules);

		if (_nModules > 0)
		{
			_modules_x = NEW short[_nModules];
			_modules_y = NEW short[_nModules];
			_modules_w = NEW short[_nModules];
			_modules_h = NEW short[_nModules];

			for (s32 i = 0; i < _nModules; i++)
			{
				byte mtype = ReadByteFromRawData();
 				if (mtype == 0xFF) // -> MD_RECT
 				{ 
 					if ((bs_flags & BS_MODULES_WH_SHORT)!=0)
						index += 8;
					else
						index += 6;
 				}
				else if (mtype == 0xFE) // -> MD_FILL_RECT
 				{ 
 					if ((bs_flags & BS_MODULES_WH_SHORT)!=0)
						index += 8;
					else
						index += 6;
 				}
				else if (mtype == 0xFD) // -> MD_MARKER
 				{ 
 					if ((bs_flags & BS_MODULES_WH_SHORT)!=0)
						index += 4;
					else
						index += 2;
 				}
				else if (mtype == 0xFC) // -> MD_ARC
 				{ 
 					if ((bs_flags & BS_MODULES_WH_SHORT)!=0)
						index += 12;
					else
						index += 10;
 				}
 				else if (mtype == 0xFB) // -> MD_FILL_ARC
 				{ 
 					if ((bs_flags & BS_MODULES_WH_SHORT)!=0)
						index += 12;
					else
						index += 10;
 				}
				else if (mtype == 0xFA) // -> MD_TRIANGLE
 				{ 
					index += 12;
 				}
				else if (mtype == 0xF9) // -> MD_FILL_TRIANGLE
 				{ 
					index += 12;
 				}
 				else if (mtype == 0) // -> MD_IMAGE
				{
					if ((bs_flags & BS_MODULES_IMG)!=0)
					{
						index++;//TODO	
					}

					if ((bs_flags & BS_MODULES_XY_SHORT)!=0)
					{
						_modules_x[i] = ReadShortFromRawData();
						_modules_y[i] = ReadShortFromRawData();
					}
 					else	if ((bs_flags & BS_MODULES_XY)!=0)
 					{
 						_modules_x[i] = (short)ReadByteFromRawData();
 						_modules_y[i] = (short)ReadByteFromRawData();
 					}
					
					if ((bs_flags & BS_MODULES_WH_SHORT)!=0)
					{
						_modules_w[i] = ReadShortFromRawData();
						_modules_h[i] = ReadShortFromRawData();
					}
					else
					{
						_modules_w[i] = (short)ReadByteFromRawData();
						_modules_h[i] = (short)ReadByteFromRawData();
					}
				}
			}
		}

		//3d stuff
		//_vertices = NEW video::S3DVertex[_nModules][4];
	}

	//FModules
	if ((bs_flags & BS_FRAMES) != 0) 
	{
		nFModules = ReadShortFromRawData();

 		//if (AURORA_DEBUG) TRACE("AURORA: nFModules = " + nFModules);

		if (nFModules > 0) 
		{
			s32 _nFModuleslength;
			s32 fm_module_length = 0;
			if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
			{
				fm_module_length = 7;
			} 
			else 
			{
				fm_module_length = 4;
			}

			if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
			{
				fm_module_length += 6;
			}

			_nFModuleslength = nFModules*fm_module_length;
			_nFModules = NEW byte[_nFModuleslength];

			ReadFromRawData(_nFModules,_nFModuleslength);

		}

		/*if (nFModules > 0) 
		{
			_fmodules_id = NEW byte[nFModules];
			_fmodules_ox = NEW short [nFModules];
			_fmodules_oy = NEW short [nFModules];
			_fmodules_flags = NEW byte[nFModules];

			for(s32 i = 0; i < nFModules; i++)
			{
				_fmodules_id[i] = ReadByteFromRawData();

				if ((bs_flags & BS_FM_OFF_SHORT) != 0)
				{
					_fmodules_ox[i] = ReadShortFromRawData();
					_fmodules_oy[i] = ReadShortFromRawData();
				}
				else
				{
					_fmodules_ox[i] = (short)ReadByteFromRawData();
					_fmodules_oy[i] = (short)ReadByteFromRawData();
				}

				if ((bs_flags & BS_FM_PALETTE) != 0)
					index++;

				_fmodules_flags[i] = ReadByteFromRawData();

				if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
				{
					index += 6;
				}
			}
		}*/

		//Frames
		if( (bs_flags & BS_FRAME_RECTS) != 0 )
		{
			s32 rectCount = ReadShortFromRawData();

			if( (bs_flags & BS_FM_OFF_SHORT) != 0 ) 		
			{
				_frame_rects = NEW s16[rectCount << 2];
				ReadFromRawData( _frame_rects, (rectCount << 2) * sizeof(s16) ); 
			}
			else
			{
				_frame_rects = NEW s16[rectCount << 2];
				for( s32 i=0; i<(rectCount << 2); i++ )
				{
					_frame_rects[i] = ReadByteFromRawData();
				}
			}
		}

		s32 nFrames = ReadShortFromRawData();
		_nFrames = nFrames;

 		//if (AURORA_DEBUG) TRACE("AURORA: nFrames = " + nFrames);

		if (nFrames > 0) 
		{
			short frame_rects_offset = 0;
			{		
				_frames_nfm = NEW short[nFrames];
				_frames_fm_start = NEW short[nFrames];

				if( (bs_flags & BS_FRAME_RECTS) != 0 )
				{
					_frames_frect_start = NEW u16[nFrames];
					_frames_frect_num = NEW u8[nFrames];
				}
				
				s32 frect_offset = 0;

				for (s32 i = 0; i < nFrames; i++) 
				{
					if ((bs_flags & BS_NFM_1_BYTE) != 0)
					{
						_frames_nfm[i] = ReadByteFromRawData();
					}
					else
					{
						_frames_nfm[i] = ReadShortFromRawData();
					}

					_frames_fm_start[i] = ReadShortFromRawData();

					if( (bs_flags & BS_FRAME_RECTS) != 0 ) 
					{
						_frames_frect_num[i] = ReadByteFromRawData();
						_frames_frect_start[i] = frect_offset;

						frect_offset += _frames_frect_num[i];
					}
				}
			}                                 
                                              
			if ((bs_flags & BS_SKIP_FRAME_RC) == 0) 
			{
				if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
				{
					_frames_rc = NEW byte[nFrames<<3];
					ReadFromRawData(_frames_rc,nFrames<<3);
				}
				else
				{
					_frames_rc = NEW byte[nFrames<<2];
					ReadFromRawData(_frames_rc,nFrames<<2);
				}
			}                                 
                                              
			if ((bs_flags & BS_FRAME_COLL_RC) != 0) 
			{
				if ((bs_flags & BS_FM_OFF_SHORT) != 0)
				{
					_frames_col = NEW byte[nFrames<<3];
					ReadFromRawData(_frames_col,nFrames<<3);
				}
				else
				{
					_frames_col = NEW byte[nFrames<<2];
					ReadFromRawData(_frames_col,nFrames<<2);
				}
			}                                 
		}                                    
	}


	//AFrames
	if ((bs_flags & BS_ANIMS) != 0) 
	{
		_nAFrames = ReadShortFromRawData();

 		//if (AURORA_DEBUG) TRACE("AURORA: nAFrames = " + _nAFrames);

		if (_nAFrames > 0) 
		{
			s32 _nAFramesLength;
			if ((bs_flags & BS_AF_OFF_SHORT) != 0)
				_nAFramesLength = 7;
			else
				_nAFramesLength = 5;

			_aframes = NEW byte[_nAFrames*_nAFramesLength];

			ReadFromRawData(_aframes,_nAFrames*_nAFramesLength);
		}

		//Animsations

		s32 nAnims = ReadShortFromRawData();

		_nAnims = nAnims;
 		//if (AURORA_DEBUG) TRACE("AURORA: nAnims = " + nAnims);

		if (nAnims > 0) 
		{
			_anims_naf = NEW short[nAnims];
			_anims_af_start = NEW short[nAnims];
			for (s32 i = 0; i < nAnims; i++) 
			{
				if ((bs_flags & BS_NAF_1_BYTE) != 0)
				{
					_anims_naf[i] = ReadByteFromRawData();
				}
				else
				{
					_anims_naf[i] = ReadShortFromRawData();
				}

				_anims_af_start[i] = ReadShortFromRawData();
			}
		}
	}

	if (_nModules <= 0) 
	{
 		//if (AURORA_DEBUG) TRACE("AURORA: WARNING - This sprite has 0 nModules");
		return false;
	}

	return true;
}

////////////////////////////////////////////////////////////////////////////////////////////////////

void ASprite::PaintAFrame(s32 anim, s32 aframe, s32 posX, s32 posY, s32 flags, s32 hx, s32 hy ,s32 alpha, bool is3d)
{
	s32 off = (_anims_af_start[anim] + aframe) * 5;
	s32 frame = _aframes[off] & 0xFF;

	if (USE_INDEX_EX_AFRAMES) {
		frame |= ((_aframes[off + 4] & FLAG_INDEX_EX_MASK) << INDEX_EX_SHIFT);
	}
	s32 temp_hx = static_cast<s8>(_aframes[off + 2]);
	s32 temp_hy = static_cast<s8>(_aframes[off + 3]);

	if ((flags & FLAG_FLIP_X) != 0) hx += temp_hx;
	else hx -= temp_hx;
	if ((flags & FLAG_FLIP_Y) != 0) hy += temp_hy;
	else hy -= temp_hy;

	PaintFrame( frame, posX - hx, posY - hy, flags ^ (_aframes[off + 4] & 0x0F), hx, hy, alpha, 0, 100, 100, is3d);

}

void ASprite::PaintFModule( s32 frame, s32 fmodule, s32 posX, s32 posY, s32 flags, s32 hx, s32 hy, s32 alpha, s32 rotate, s32 xscale, s32 yscale, bool is3d)
{
	s32 off;
	s32 fm_flags;

	s32 ex_off = 0;
	if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
	{
		if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
			off = (_frames_fm_start[frame] + fmodule) * 12;
		else
			off = (_frames_fm_start[frame] + fmodule) * 7;
		fm_flags = _nFModules[off + 6] & 0xFF;
	} 
	else 
	{
		off = (_frames_fm_start[frame] + fmodule) << 2;
		fm_flags = _nFModules[off + 3] & 0xFF;
	}

	s32 index = (short)((_nFModules[off + 0] & 0xFF) + ((_nFModules[off + 1] & 0xFF) << 8));
	if (USE_INDEX_EX_FMODULES) 
	{
		index |= ((fm_flags & FLAG_INDEX_EX_MASK) << INDEX_EX_SHIFT);
	}

	s32 temp_hx = (short)((_nFModules[off + 2] & 0xFF) + ((_nFModules[off + 3] & 0xFF) << 8));
	s32 temp_hy = (short)((_nFModules[off + 4] & 0xFF) + ((_nFModules[off + 5] & 0xFF) << 8));
	
	if ((flags & FLAG_FLIP_X) != 0) posX -= temp_hx;
	else posX += temp_hx;
	if ((flags & FLAG_FLIP_Y) != 0) posY -= temp_hy;
	else posY += temp_hy;
	
	if (USE_HYPER_FM && ((fm_flags & FLAG_HYPER_FM) != 0)) 
	{
		PaintFrame(index, posX, posY, flags ^ (fm_flags & 0x0F), hx, hy, alpha, rotate, xscale, yscale, is3d);
	} 
	else 
	{
		if ((flags & FLAG_FLIP_X) != 0) posX -= _modules_w[index]/* & 0xFF*/;
		if ((flags & FLAG_FLIP_Y) != 0) posY -= _modules_h[index]/* & 0xFF*/;

		if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) == 0)
			PaintModule(index, posX, posY, flags ^ (fm_flags & 0x0F), rotate, xscale, yscale, alpha, is3d);
		else
		{
			s32	s_rotate = (short)((_nFModules[off + 6] & 0xFF) + ((_nFModules[off + 7] & 0xFF) << 8));
			s32	s_xscale = (short)((_nFModules[off + 8] & 0xFF) + ((_nFModules[off + 9] & 0xFF) << 8));
			s32	s_yscale = (short)((_nFModules[off + 10] & 0xFF) + ((_nFModules[off + 11] & 0xFF) << 8));
			PaintModule(index, posX, posY, flags ^ (fm_flags & 0x0F), s_rotate + rotate, s_xscale * xscale / 100, s_yscale * yscale / 100, alpha, is3d);
		}
	}

}

void ASprite::PaintModule(s32 module, s32 posX, s32 posY, s32 flags, s32 rotate, s32 xscale, s32 yscale, s32 alpha, bool is3d)
{
	//A_ASSERT(module < _nModules);
	s32 srcPosX = _modules_x[module];
	s32 srcPosY = _modules_y[module];
	s32 sizeX = _modules_w[module];// & 0xFF;
	s32 sizeY = _modules_h[module];// & 0xFF;

	if (sizeX <= 0 || sizeY <= 0)
		return;

	//*TODO : need to implement	
	//./CGfx *g = CGfx::GetInstance();
	if (_modules_image != NULL)
	{
		IVideoDriver * driver = Application::GetInstance()->GetDevice()->getVideoDriver();

		r2i destRect = r2i(posX, posY, posX + sizeX, posY + sizeY);
		r2i srcRect = r2i(srcPosX, srcPosY, srcPosX + sizeX, srcPosY + sizeY);
		r2i* clipRect = s_clipRectStack.size() > 0 ? &s_clipRectStack.getLast() : 0;

		if (flags & FLAG_FLIP_X)
		{
			std::swap(srcRect.LowerRightCorner.X, srcRect.UpperLeftCorner.X);
		}

		if (flags & FLAG_FLIP_Y)
		{
			std::swap(srcRect.LowerRightCorner.Y, srcRect.UpperLeftCorner.Y);
		}

		SColor color = _color;
		color.A = (color.A * alpha) >> 8;

		SColor colors[4] = { color, color, color, color };

		bool freeRotScale = (rotate != 0 || xscale != 100 || yscale != 100);
		if (freeRotScale)
		{
			vector3df rot(0.0f, 0.0f, Float::toRadian(rotate));
			vector3df scale((f32)xscale/100, (f32)yscale/100, 1.0f);
			vector3df pivot(posX, posY, 0.0f);
			ASprite::PushTransform(rot, scale, pivot);
		}

		if (is3d)
		{
			float dx1 = posX, dx2 = posX + sizeX;
			float dy1 = posY, dy2 = posY + sizeY;
			float sx1 = srcRect.UpperLeftCorner.X, sx2 = srcRect.LowerRightCorner.X;
			float sy1 = srcRect.UpperLeftCorner.Y, sy2 = srcRect.LowerRightCorner.Y;
			float w = _modules_image->getSize().Width;
			float h = _modules_image->getSize().Height;

			S3DVertex vertices[] =
			{
				S3DVertex(v3f(dx1, dy1, 0.0f), v3f(), color, v2f(sx1 / w, sy1 / h)),
				S3DVertex(v3f(dx2, dy1, 0.0f), v3f(), color, v2f(sx2 / w, sy1 / h)),
				S3DVertex(v3f(dx1, dy2, 0.0f), v3f(), color, v2f(sx1 / w, sy2 / h)),
				S3DVertex(v3f(dx2, dy2, 0.0f), v3f(), color, v2f(sx2 / w, sy2 / h))
			};
			u16 indices[] = { 0, 2, 1, 1, 2, 3 };
			driver->setMaterial(*_material);
			driver->drawVertexPrimitiveList(vertices, indices, 0, 6, 2, EVT_STANDARD, EPT_TRIANGLES, EIT_16BIT);
		}
		else
		{
			driver->set2DMaterial(*_material);
			driver->draw2DImage(_modules_image, destRect, srcRect, clipRect, colors, true);
		}

		if (freeRotScale)
		{
			ASprite::PopTransform();
		}
	}
}

///////////////////////////////////////////////////////////////////////////////////////////////////////////////
// 3D drawing
///////////////////////////////////////////////////////////////////////////////////////////////////////////////

void SetMatrix (v3f pos, float xs, float ys)
{
	ICameraSceneNode* cam = __IRR_SCENE()->getActiveCamera();
	v3f view = cam->getTarget() - cam->getAbsolutePosition();

	matrix4 m1, m2;
	m1.buildCameraLookAtMatrix(pos, pos - view, -cam->getUpVector());
	m1.getInverse(m2);
	m1 = m2;
	m1.postScale(v3f(xs, ys, 1.0f));

	__IRR_DRIVER()->setTransform(ETS_WORLD, m1);
}

void ASprite::PaintAFrame3D(s32 anim, s32 aframe, const core::vector3df& pos, s32 flags, s32 hx, s32 hy, f32 xscale, f32 yscale)
{
	SetMatrix(pos, xscale, yscale);
	
	PaintAFrame(anim, aframe, 0, 0, flags, hx, hy, 255, true);
}

void ASprite::PaintFrame3D(s32 frame, const core::vector3df& pos, s32 flags, s32 hx, s32 hy, f32 xscale, f32 yscale)
{
	SetMatrix(pos, xscale, yscale);

	PaintFrame(frame, 0, 0, flags, hx, hy, 255, 0, 100, 100, true);
}

s32 ASprite::UpdateStringSize(TChar* s )
{
	_text_w = 0;
	_text_h = _line_height;
	s32 tw = 0;
	// TODO: Spyder
	s32 tmpEscapeDigit = ' '; //Application::GetInstance()->GetStringManager()->GetCurrentEscapeDigit();

	s32 index1 = ((_index1 >= 0) ? _index1 : 0);
	s32 index2 = ((_index2 >= 0) ? _index2 : StrLength(s));

	s32 prev_c = -1;
	for (s32 i = index1; i < index2; i++) 
	{
		TChar c = s[i] & 0xFFFF; 

		if(c == ESCAPE_CHARACTER_PREFIX)
		{
			TChar nextChar = s[++i];

			// TODO: Spyder
			TChar* str1 = L" "; //Application::GetInstance()->GetStringManager()->GetSpecialCharacterString(nextChar);
			tw += UpdateStringSize(str1);
			continue;
		}

		tw += GetCharacterWidth(c) + _char_spacing;
	}

	if (tw > _text_w)
		_text_w = tw;

	// TODO: Spyder
	//Application::GetInstance()->GetStringManager()->SetCurrentEscapeDigit(tmpEscapeDigit);

	return tw;
}

s32 ASprite::DrawString( TChar* s, s32 x, s32 y, s32 anchor ,s32 alpha /*= 255*/,s32 rotate /*= 0*/, s32 x_scale /*= 100 */, s32 y_scale /*= 100*/, bool WB , bool WH )
{
	float	x_scale_factor = x_scale / 100.0f;
	float	y_scale_factor = y_scale / 100.0f;

	const TChar* ptr = s;
	y -= GetFrameTop(0);

	if ((anchor & (RIGHT | HCENTER | BOTTOM | VCENTER)) != 0) 
	{
		UpdateStringSize(s);
		if ((anchor & RIGHT) != 0)
			x -= (s32)(_text_w * x_scale_factor);
		else if ((anchor & HCENTER) != 0)
			x -= (s32)((_text_w >> 1)  * x_scale_factor);
		if ((anchor & BOTTOM) != 0)
			y -= (s32)(_text_h * y_scale_factor);
		else if ((anchor & VCENTER) != 0)
			y -= (s32)((_text_h >> 1) * y_scale_factor);
	}

	s32 xx = x;
	s32 yy = y;

	s32 old_pal = _crt_pal;

	s32 index1 = ((_index1 >= 0) ? _index1 : 0);
	s32 index2 = ((_index2 >= 0) ? _index2 : StrLength(s));

	s32 nextChar;
	for (s32 i = index1; i < index2; i++) 
	{
		s32 c = s[i] & 0xFFFF;

		if(c == ESCAPE_CHARACTER_PREFIX)
		{
			nextChar = s[++i];
					
			//Reset substring anchor flags to Top, Left since we allready did the transformation for parent string.
			// TODO: Spyder
			TChar* str1 = L" "; //Application::GetInstance()->GetStringManager()->GetSpecialCharacterString(nextChar);
			xx += DrawString(str1, xx, yy, LEFT | TOP);
			continue;
		}
		else if (c == ' ') 
		{
			xx += _space_width + _char_spacing;
			continue;
		}

		s32 frame = GetCharFrame(c);
		PaintFrame(frame, xx, yy, 0, 0, 0, alpha, rotate, x_scale, y_scale);

		xx += GetFrameWidth(frame) + _char_spacing;
	}

	_crt_pal = old_pal;
	_index1 = -1;
	_index2 = -1;

	return xx - x;
}

void ASprite::UpdatePage( TChar* s, s32 width )
{
	s32 tw = 0;
	s32 th = 0;
	s32 tw_old = 0;
	s32 len = StrLength(s);

	lines = 0;
	// Wrap text in multiple lines depending on the value of width
	for (s32 i = 0; i < len; i++) 
	{
		s32 c = s[i] & 0xFFFF;

		if (c == ' ') 
		{
			tw_old = tw;
			off[lines] = i;
			tw += _space_width + _char_spacing;
			continue;
		}
		else if(c == '\n')
		{
			tw = 0;
			tw_old = 0;
			off[lines++] = i;
			continue;
		}

		s32 frame = GetCharFrame(c);
		tw += GetFrameWidth(frame) + _char_spacing;

		if (tw > width && tw_old != 0) 
		{
			tw = tw - tw_old - _char_spacing;
			tw_old = 0;

			lines++;
		}
	}

	// Get last offset
	off[lines++] = len;
}

s32 ASprite::GetPageHeight(TChar* s,s32 width)
{
	s32 total_height = 0;
	UpdatePage(s, width);
	s32 th = _line_spacing + GetFrameHeight(33);
	total_height = th * lines - _line_spacing;
	return total_height;
}


s32 ASprite::DrawPage( /*Graphics g, */TChar* s, s32 x, s32 y, s32 width, s32 start, s32 line_number, s32 character_number, s32 anchor, s32 type, bool bUpdate , bool WB)
{
	s32 th = 0;
	s32 total_height = 0;

	if (bUpdate)
	{
		UpdatePage(s, width);
	}

	// Get line distances
	th = _line_spacing + GetFrameHeight(0);
	if (type == 2 || type == 3)
		total_height = th * line_number - _line_spacing;
	else
		total_height = th * lines - _line_spacing;

	if (type == 0 || type == 2)
	{
		if ((anchor & VCENTER) != 0) 
		{
			y -= total_height >> 1;
		} 
		else if ((anchor & BOTTOM) != 0) 
		{
			y -= total_height;
		}

		if (type == 0)
		{
			start = 0;
			line_number = lines;
		}

		s32 old_pal = _crt_pal;
		s32 _current_charater_number = 0;

		// Draw the text lines
		for (s32 j = start; j < (start + line_number > lines ? lines : start + line_number); j++) 
		{
			if (j < 0) continue;

			_index1 = (j > 0) ? off[j - 1] + 1 : 0;
			_index2 = off[j];
			if(character_number != -1 && (_current_charater_number + _index2 - _index1) > character_number)
			{
				_index2 = _index1 + (character_number - _current_charater_number);
			}
			_current_charater_number += (_index2 - _index1);
			DrawString(/*g,*/ s, x, y + (j - start) * th, (anchor & (LEFT|HCENTER|RIGHT)) | TOP,255,0,100,100,WB);
		}

		_crt_pal = old_pal;
		// Reset substring feature
		_index1 = -1;
		_index2 = -1;
	}

	if (type == 2 || type == 3)
		return lines;
	else
		return total_height;
}

s32 ASprite::GetCharSpacing(s32 index)
{
    if(GetFrameColWidth(index) == 0)
    {
        return _char_spacing;
    }
    else
    {
        return 0;
    }
}
s32 ASprite::GetFontFrameWidth(s32 frame)
{
    if(GetFrameColWidth(frame) == 0)
    {
        return GetFrameWidth(frame);
    }
    else
    {
        return GetFrameColWidth(frame);
    }
}

s32 ASprite::GetFontFrameLeft(s32 frame)
{
    s32 a = GetFrameColLeft(frame);
    return GetFrameColLeft(frame);
}

s32 ASprite::GetFModuleOffX(s32 frame, s32 fmodule)
{
	s32 off;

	s32 ex_off = 0;
	if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
	{
		if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
			off = (_frames_fm_start[frame] + fmodule) * 12;
		else
			off = (_frames_fm_start[frame] + fmodule) * 7;
	} 
	else 
	{
		off = (_frames_fm_start[frame] + fmodule) << 2;
	}

	return ((_nFModules[off + 2] & 0xFF) + ((_nFModules[off + 3] & 0xFF) << 8));
}

s32 ASprite::GetFModuleOffY(s32 frame, s32 fmodule)
{
	s32 off;

	s32 ex_off = 0;
	if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
	{
		if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
			off = (_frames_fm_start[frame] + fmodule) * 12;
		else
			off = (_frames_fm_start[frame] + fmodule) * 7;
	} 
	else 
	{
		off = (_frames_fm_start[frame] + fmodule) << 2;
	}

	return ((_nFModules[off + 4] & 0xFF) + ((_nFModules[off + 5] & 0xFF) << 8));
}

s32 ASprite::GetFModuleWidth(s32 frame, s32 fmodule)
{
	s32 off;
	if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
	{
		if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
			off = (_frames_fm_start[frame] + fmodule) * 12;
		else
			off = (_frames_fm_start[frame] + fmodule) * 7;
	} 
	else 
	{
		off = (_frames_fm_start[frame] + fmodule) << 2;
	}
	if( frame == 0 && fmodule == 330 )
	{
		int a=0;
		a++;
	}
	s32 module = (short)((_nFModules[off + 0] & 0xFF) + ((_nFModules[off + 1] & 0xFF) << 8));;
	return _modules_w[module];
}

s32 ASprite::GetFModuleHeight(s32 frame, s32 fmodule)
{
	s32 off;
	if ((bs_flags & BS_FM_OFF_SHORT) != 0) 
	{
		if ((ex_bs_flags & BS_FM_FREE_ROTATE_SCALE) != 0)
			off = (_frames_fm_start[frame] + fmodule) * 12;
		else
			off = (_frames_fm_start[frame] + fmodule) * 7;
	} 
	else 
	{
		off = (_frames_fm_start[frame] + fmodule) << 2;
	}

	s32 module = (short)((_nFModules[off + 0] & 0xFF) + ((_nFModules[off + 1] & 0xFF) << 8));
	return _modules_h[module];
}

void ASprite::LoadCharacterMap()
{
	if(_map_char == null)
	{
		//this should be enough for europen languages
		_map_char = NEW short[65535];
		memset(_map_char, 0, sizeof(short)*65535);
	}

	// TODO: Spyder
	TChar* characterMapString = L" "; //Application::GetInstance()->GetStringManager()->GetString(GLOBAL_CHARACTER_MAPPING);

	s32 length = StrLength(characterMapString);

	TChar c;
	for (s32 i = 0; i < length; ++i)
	{
		c = characterMapString[i];
		_map_char[(c & 0xFFFF)] = (short) i;
	}
}

s32 ASprite::GetCharFrame(TChar c)
{
	if (c < 0)
	{
		c = (c & 0xFFFF);
	}
	
	if( c < 0 || c >= 65535)
	{			
		c = ' ';
	}
	
	GX_ASSERT( _map_char[c] >= 0 );
	
	if(_map_char[c] < 0 )
	{			
		c = '?';
	}
	return _map_char[c];
}

s32 ASprite::GetCharacterWidth(TChar c)
{
	//Special cases character.
	if(c == '\n')
	{
		return 0;
	}
	
	s32 frame = GetCharFrame(c);
	return GetFrameWidth(frame);
}

void ASprite::InitSpaceSizeWithAuroraValue()
{
	_space_width = GetCharacterWidth(' ');
	_line_height = GetModuleHeight(0);
}

s32 ASprite::StrLength(TChar* s)
{
	//s32 index = 0;
	//while(s[index] !=0) index++;
	//return index;
	
	return wcslen(s);
}

////////////////////////////////////////////////////////////////////////////////////////////////////
/// TRANSFORM 2D

array<matrix4> ASprite::s_transformStack;

void ASprite::PushTransform(vector3df pivot, vector3df scale, vector3df rotation)
{
	matrix4 m;

	m *= matrix4().setTranslation(pivot);
	m *= matrix4().setRotationRadians(rotation);
	m *= matrix4().setScale(scale);
	m *= matrix4().setTranslation(-pivot);

	PushTransform(m);
}

void ASprite::PushTranslation(vector3df offset)
{
	matrix4 m;
	m.setTranslation(offset);
	PushTransform(m);
}

void ASprite::PushTransform(matrix4 & m)
{
	const matrix4 & old = __IRR_DRIVER()->getTransform(ETS_WORLD);

	s_transformStack.push_back( old );
	__IRR_DRIVER()->setTransform(ETS_WORLD, old * m);
}

void ASprite::PopTransform()
{
	const matrix4 & old = s_transformStack.getLast();

	__IRR_DRIVER()->setTransform(ETS_WORLD, old);

	int last = s_transformStack.size() - 1;
	s_transformStack.erase(last);
}

////////////////////////////////////////////////////////////////////////////////////////////////////
// Clip Rect

array<r2i> ASprite::s_clipRectStack;

void ASprite::PushClipRect(r2i r)
{
	if (s_clipRectStack.empty())
	{
		s_clipRectStack.push_back(r);
	}
	else
	{
		r.clipAgainst(s_clipRectStack.getLast());
		s_clipRectStack.push_back(r);
	}
}

void ASprite::PopClipRect()
{
	int last = s_clipRectStack.size() - 1;
	s_clipRectStack.erase(last);
}

////////////////////////////////////////////////////////////////////////////////////////////////////
