/**
--- Structural indicators --- 

\addtogroup  groupNameWithTitle	Some group title
\addtogroup groupNameWithoutTitle
\callgraph
\hidecallgraph
@callergraph
\hidecallergraph
\showrefby
\hiderefby
\showrefs
\hiderefs
\category Test1 class.h inc/class.h
\class Test1 class.h "inc dir/class.h"
\class Test2 class.h
\class Test3
\concept concept_name
\def MAX(x,y)
\defgroup IntVariables Global integer variables
\dir path with spaces\example_test.cpp
\enum Enum_Test::TEnum
\example example_test.cpp
\example{lineno} path with spaces\example_test.cpp
\endinternal
\extends Object
\file path with spaces\example_test.cpp
\fileinfo{file}
\fileinfo{extension}
\fileinfo{filename}
\fileinfo{directory}
\fileinfo{full}
\lineinfo
\fn const char *Fn_Test::member(char c,int n)
\headerfile test.h "some name"
\headerfile "test.h" some name
\headerfile test.h ""
\headerfile test.h
\headerfile "test.h"
\headerfile ""  
\headerfile test.h <test.h>
\headerfile test.h <>
\headerfile <>
\hideinitializer
\idlexcept exception
\implements ISomeInterface_
\ingroup Group1
\ingroup Group1 Group2 Group3
\interface  Test1 class.h inc/class.h
\internal
\mainpage My Personal Index Page
\memberof _some_name
\name group_
\namespace nested::space
\nosubgrouping
\overload void Overload_Test::drawRect(const Rect &r)
\package PackageName
\page page1 A documentation page
\private
\privatesection
\property   const char *Fn_Test::member(char c,int n)
\protected  
\protectedsection  
\protocol ProtocolName Header.h inc/Header.h
\public
\publicsection
\pure
\relates String
\related String
\relatesalso  String  
\relatedalso  String
\showinitializer
\static  
\typedef unsigned long ulong
@struct Test1 class.h "inc dir/class.h"
\union Test1 class.h "inc dir/class.h"
\var unsigned long variable
\weakgroup groupNameWithTitle Some group title


--- Section indicators ---
\attention some text
\author authors
\authors authors
\brief Some description
\bug some bug description
\cond   
\cond (!LABEL1 && LABEL2)
\copyright Some copyright
\date 1990-2011
\showdate "%A %d-%m-%Y %H:%M:%S"  2015-3-14 03:04:15
\showdate "%A %d-%m-%Y %H:%M:%S"  "2015-3-14 03:04:15"
\showdate  "%A %d-%m-%Y %H:%M:%S"
\deprecated Some deprecated stuff
\details Some details
\noop some stuff
\raisewarning My specific warning
\else
\elseif (!LABEL1 && LABEL2)
\endcond
\endif
\exception std::out_of_range parameter is out of range.
\if (!LABEL1 && LABEL2)
\if Cond1
\ifnot  (!LABEL1 && LABEL2)
\invariant Some text
\note Some note
\par User defined paragraph:
\par
\par:  some title
\param[out] dest The memory area to copy to.
\param[in]  src  The memory area to copy from.
\param[in]  n    The number of bytes to copy
\param[in,out] p In and out param
\param p some param
\param x,y,z Coordinates of the position in 3D space.
\parblock  
\endparblock
\tparam some_param Description of template
\post some description
\pre some description
\remark some remark
\remarks some remarks
\result Some result
\return Some return value
\returns Some return value
\retval some_value Some return value
\sa cls::ref, someFunc()
\see cls::ref, someFunc()
\short Some description
\since some date
\test Description of test
\throw std::out_of_range parameter is out of range.
\throws someException desc
@throws  std::runtime_error  Function @ref someFunc() can throw
\todo need to do something
\version v1.2
\warning some warning


--- Commands to create links ---
\addindex some text
\anchor some_word
\cite  some_label
\link link_obj some text \endlink
\endlink more text
This page contains \ref subsection1 and \ref subsection2.
See \ref link_text "some text" and more. See \ref someFunc() "some text 2" and more.
\ref link_text3 "some"
\ref anotherFunc()
\ref link_text5 not formatted because not quotes
*/
