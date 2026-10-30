#!/usr/bin/env python

import sys
import optparse
import copy
import os.path

scriptname = os.path.basename( sys.argv[ 0 ] )

# Setup ------------------------------------------------------------------------

sys.path.append( os.path.dirname( sys.argv[ 0 ] ) )
from TextureConverter import *
from TextureConverter.defs import *

# Process command line arguments -----------------------------------------------

target_name_map = {}

for t in dir() :
    if t.endswith( "Converter" ) and not t.startswith( "Base" ) :
        target_name_map[ t[ : len( t ) - len( "Converter" ) ].lower() ] \
            = globals()[ t ]

def check_target( option ,
                  opt ,
                  value ) :
    try :
        return target_name_map[ value ]
    except :
        raise optparse.OptionValueError( "unknown target: " + value )

def check_warning_behavior( option ,
                            opt ,
                            value ) :
    if value not in BaseConverter.warning_behaviors :
        raise optparse.OptionValueError( "unknown warning behavior:" + value )
    return value

class TextureConverterOption ( optparse.Option ) :
    TYPES = optparse.Option.TYPES + ( "target" , "warning_behavior" )
    TYPE_CHECKER = copy.copy( optparse.Option.TYPE_CHECKER )
    TYPE_CHECKER[ "target" ] = check_target
    TYPE_CHECKER[ "warning_behavior" ] = check_warning_behavior

opts = optparse.OptionParser( usage = "Usage: %s [options] input-files..." ,
                              option_class = TextureConverterOption )

opts.add_option( "-t" ,
                 "--target" ,
                 dest = "target" ,
                 type = "target" ,
                 default = None ,
                 help = ("Sets target platform/format. Available targets: "
                         + ", ".join( sorted( target_name_map.keys() ) ) + ".") )

opts.add_option( "-o" ,
                 "--output" ,
                 dest = "output" ,
                 default = None ,
                 help = ("Sets output file name. If not present, output file "
                         + "path is identical to input with extension replaced."
                         + " It can not be specified if more than one input "
                         + "file is given.") )

opts.add_option( "--py-impl" ,
                 dest = "py_impl" ,
                 action = "store_true" ,
                 default = False ,
                 help = ("When possible, use Python implementation instead of "
                         + "external program for conversion.") )

opts.add_option( "-c" ,
                 "--keep-intermediate-files" ,
                 dest = "cleanup" ,
                 action = "store_false" ,
                 default = True ,
                 help = ("Indicates to keep any generated intermediate files "
                         + "if possible.") )

opts.add_option( "--extra-options" ,
                 dest = "extra_options" ,
                 default = "" ,
                 help = ("Specify additional options to pass to the underlying "
                         + "program used for main conversion (if one is used).") )

opts.add_option( "-f" ,
                 "--format" ,
                 dest = "format" ,
                 default = None ,
                 help = "Specify texture format." )

opts.add_option( "-V" ,
                 "--vertical-flip" ,
                 dest = "vertical_flip" ,
                 action = "store_true" ,
                 default = False ,
                 help = ("Indicates to flip the image vertically before "
                         + "converting.") )

opts.add_option( "-m" ,
                 "--generate-mipmaps" ,
                 dest = "generate_mipmaps" ,
                 action = "store_true" ,
                 default = False ,
                 help = "Hint to generate complete mipmap chain." )

opts.add_option( "--debug" ,
                 dest = "debug" ,
                 action = "store_true" ,
                 default = False ,
                 help = "Turns on debugging mode." )

opts.add_option( "--verbose" ,
                 dest = "verbose" ,
                 action = "store_true" ,
                 default = False ,
                 help = "Increments verbosity of output." )

opts.add_option( "--raw" ,
                 dest = "raw_format" ,
                 action = "store_true" ,
                 default = False ,
                 help = ("For formats that actually wrap an existing format "
                         + "output the file in the raw wrapped "
                         + "format.") )

opts.add_option( "-W" ,
                 "--warning-behavior" ,
                 type = "warning_behavior" ,
                 dest = "warning_behavior" ,
                 default = "warn" ,
                 help = ("Sets the warning behavior. Possible values are: "
                         + ", ".join( sorted( BaseConverter
                                              .warning_behaviors.keys() ) )
                         + ". Default: warn.") )

opts.add_option( "--dry-run" ,
                 dest = "dry_run" ,
                 action = "store_true" ,
                 default = False ,
                 help = ("Do not execute commands, just report what will be "
                         + "done.") )

options , args = opts.parse_args()

# ensure unicity
args = sorted( set( args ) )

# check output file
if len( args ) > 1 and options.output :
    print_flush( ("%s: can not set output file name when processing more than "
                  + "one input file") % scriptname )
    sys.exit( 1 )

# check target (no defaults for now)
if not options.target :
    print_flush( "%s: missing target" % scriptname )
    sys.exit( 1 )

# Go! --------------------------------------------------------------------------

converter = options.target( options )
err = 0

for input in args :
    try :
        if options.verbose :
            print_flush( "processing " + input )
        converter( input , options.output )
    except Exception , e :
        if options.debug :
            import traceback
            traceback.print_exc( file = sys.stderr )
        print_flush( "%s: error: %s" % ( scriptname , e ) )
        err += 1

sys.exit( err )
