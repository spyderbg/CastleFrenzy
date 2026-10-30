#include "stdafx.h"
#include "CTextureManager.h"
#include "IReadFile.h"
#include "CNullDriver.h"
#include "irros.h"
#include "CImage.h"

namespace irr
{
namespace video
{

CTextureManager::CTextureManager(CNullDriver *driver)
: Driver(driver), FullPath(true)
{
}

bool 
CTextureManager::SSurface::operator < (const SSurface& other) const
{
	return Surface->getName() < other.Surface->getName();
}


//! opens the file and loads it into the surface
ITexture* 
CTextureManager::loadTextureFromFile(io::IReadFile* file, const c8 *hashName, bool refData )
{
#ifdef _IRR_XBOX_PLATFORM_
#ifdef _IRR_ENABLE_NATIVE_TEXTURE_FORMAT_
	{
	ITexture* t = Driver->createDeviceDependentNativeTextureFromFile(
				file, hashName ? hashName : file->getFileName(), refData
			);
	if( t != NULL )
	{
		os::Printer::log("Loaded texture", file->getFileName());
		return t;
	}
	}
#endif
#else
#ifdef _IRR_ENABLE_NATIVE_TEXTURE_FORMAT_
//#ifndef WIN32
	if (strstr(file->getFileName(), ".pvr"))
	{
		return Driver->createDeviceDependentNativeTextureFromFile(
			file, hashName ? hashName : file->getFileName(), refData
		);
	}
//#endif
#endif
#endif
	ITexture* texture = 0;
	IImage* image = Driver->createImageFromFile(file);

	if (image)
	{
		// create texture from surface
		texture = Driver->createDeviceDependentTexture(image, hashName ? hashName : file->getFileName() );
		os::Printer::log("Loaded texture", file->getFileName());
		image->drop();
	}

	return texture;
}



//! adds a surface, not loaded or created by the Irrlicht Engine
void 
CTextureManager::addTexture(video::ITexture* texture)
{
	if (texture)
	{
		SSurface s;
		s.Surface = texture;
		texture->grab();

		Textures.push_back(s);

		// the new texture is now at the end of the texture list. when searching for
		// the next new texture, the texture array will be sorted and the index of this texture
		// will be changed. to let the order be more consistent to the user, sort
		// the textures now already although this isn't necessary:

		Textures.sort();
	}
}


struct SDummyTexture : public ITexture
{
	SDummyTexture(const char* name) : ITexture(name), size(0,0) {};

	virtual void* lock(bool readOnly = false) { return 0; };
	virtual void unlock(){}
	virtual const core::dimension2d<s32>& getOriginalSize() const { return size; }
	virtual const core::dimension2d<s32>& getSize() const { return size; }
	virtual E_DRIVER_TYPE getDriverType() const { return video::EDT_NULL; }
	virtual ECOLOR_FORMAT getColorFormat() const { return video::ECF_A1R5G5B5; };
	virtual u32 getPitch() const { return 0; }
	virtual void regenerateMipMapLevels() {};
	core::dimension2d<s32> size;
};

//! looks if the image is already loaded
video::ITexture* 
CTextureManager::findTexture(const c8* filename)
{
	if (!filename)
		filename = "";

	SSurface s;
	SDummyTexture dummy(filename);
	s.Surface = &dummy;

	s32 index = Textures.binary_search(s);
	if (index != -1)
		return Textures[index].Surface;

	return 0;
}



//! Creates a texture from a loaded IImage.
ITexture* 
CTextureManager::addTexture(const c8* name, IImage* image)
{
	if (!name || !image)
		return 0;

	ITexture* t = Driver->createDeviceDependentTexture(image, name);
	if (t)
	{
		addTexture(t);
		t->drop();
	}
	return t;
}



//! creates a Texture
ITexture* 
CTextureManager::addTexture(const core::dimension2d<s32>& size,
				const c8* name, ECOLOR_FORMAT format)
{
	if (!name)
		return 0;

	IImage* image = irrnew CImage(format, size);
	ITexture* t = Driver->createDeviceDependentTexture(image, name);
	image->drop();
	addTexture(t);

	if (t)
		t->drop();

	return t;
}

//! Removes a texture from the texture cache and deletes it, freeing lot of
//! memory.
void 
CTextureManager::removeTexture(ITexture* texture)
{
	if (!texture)
		return;

	for (u32 i=0; i<Textures.size(); ++i)
	{
		if (Textures[i].Surface == texture)
		{
			texture->drop();
			Textures.erase(i);
		}
	}
}


//! Removes all texture from the texture cache and deletes them, freeing lot of
//! memory.
void 
CTextureManager::removeAllTextures()
{
	deleteAllTextures();
}

//! deletes all textures
void 
CTextureManager::deleteAllTextures()
{
	for (u32 i=0; i<Textures.size(); ++i)
		Textures[i].Surface->drop();
	Textures.clear();
}


//! Returns a texture by index
ITexture* 
CTextureManager::getTextureByIndex(u32 i)
{
	if ( i < Textures.size() )
		return Textures[i].Surface;

	return 0;
}


//! Returns amount of textures currently loaded
u32 
CTextureManager::getTextureCount() const
{
	return Textures.size();
}


//! Renames a texture
void 
CTextureManager::renameTexture(ITexture* texture, const c8* newName)
{
	// we can do a const_cast here safely, the name of the ITexture interface
	// is just readonly to prevent the user changing the texture name without invoking
	// this method, because the textures will need resorting afterwards

	core::stringc& name = const_cast<core::stringc&>(texture->getName());
	name = newName;

	Textures.sort();
}


//! loads a Texture
ITexture* 
CTextureManager::getTexture(const c8* filename)
{
	// Prefer PNG variants on every platform, including old model references.
	core::stringc pngFilename;
	if (filename)
	{
		core::stringc original(filename);
		const s32 dot = original.findLast('.');
		if (dot >= 0)
		{
			core::stringc extension = original.subString(dot, original.size() - dot);
			extension.make_lower();
			if (extension == ".pvr" || extension == ".tga" || extension == ".bmp")
			{
				pngFilename = original.subString(0, dot) + ".png";
				if (extension == ".pvr" || Driver->FileSystem->existFile(pngFilename.c_str()))
					filename = pngFilename.c_str();
			}
		}
	}
	irr::core::stringc absoluteFilename;
	ITexture* texture = 0;
	//pls using fullpath as false, especially you want to try load texture in your zip archive
	if(FullPath)
	{
		absoluteFilename = Driver->FileSystem->getAbsolutePath(filename);
		filename = absoluteFilename.c_str();
	}

	texture = findTexture(filename);

	if (texture)
		return texture;

	io::IReadFile* file = Driver->FileSystem->createAndOpenFile(filename);

	if (file)
	{
		texture = loadTextureFromFile(file, filename);
		file->drop();

		if (texture)
		{
			addTexture(texture);
			texture->drop(); // drop it because we created it, one grab too much
		}
		else
			os::Printer::log("Could not load texture", filename, ELL_ERROR);
		return texture;
	}
	else
	{
		os::Printer::log("Could not open file of texture", filename, ELL_WARNING);
		return 0;
	}
}


//! loads a Texture
ITexture* 
CTextureManager::getTexture(io::IReadFile* file, bool refData)
{
	ITexture* texture = 0;

	if (file)
	{
		texture = findTexture(file->getFileName());

		if (texture)
			return texture;

		texture = loadTextureFromFile(file, 0, refData);

		if (texture)
		{
			addTexture(texture);
			texture->drop(); // drop it because we created it, one grab too much
		}
	}

	if (!texture)
		os::Printer::log("Could not load texture", file->getFileName(), ELL_WARNING);

	return texture;
}

} // end namespace video
} // end namespace irr

