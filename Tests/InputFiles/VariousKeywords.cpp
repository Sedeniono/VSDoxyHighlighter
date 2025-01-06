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
@showinlinesource
@hideinlinesource
\includegraph
@hideincludegraph
@includedbygraph
\hideincludedbygraph
\directorygraph
\hidedirectorygraph
\collaborationgraph
\hidecollaborationgraph
\inheritancegraph
\inheritancegraph{no}
@inheritancegraph{  YES  }
\inheritancegraph{text}
\inheritancegraph{GRAPH }
\inheritancegraph{ Builtin}
\inheritancegraph {yes} Only partial highlight because space before { is not allowed.
\inheritancegraph{unknown} No highlight because of unknown option.
\hideinheritancegraph
\groupgraph
\hidegroupgraph
This\showenumvalues command should be highlighted.
This \showenumvalues command should be highlighted.
Nothing\showenumvaluesshould be highlighted.
@hideenumvalues
text \qualifier quali single word should be matched
text \qualifier "SOMEQUALI text" quote should be matched
text\qualifier "Another" quote should be matched
text\qualifier "yet"more quote should be matched
text \qualifier"more text" quote should be matched
\qualifierasdsa should not match
\qualifier "" quotes should be matched
\qualifier
\category Test1 class.h inc/class.h
\class Test1 class.h "inc dir/class.h"
\class Test2	class.h
\class Test3::MemClass
\class Test4  	 
\class Test5
  "NoFormatting"
\class  
  No formatting
\class
  No formatting
\class
\concept concept_name
\def MAX(x,y)
\def   
\def
   nothing to match
\defgroup IntVariables Global integer variables
\dir path with spaces\example_test.cpp
\enum Enum_Test::TEnum
\example example_test.cpp
\example{lineno} path with spaces\example_test.cpp
\endinternal
\extends Object
\file path with spaces\example_test.cpp
Text \fileinfo more text
Some text \fileinfo{name} more text
Some text \fileinfo{extension}
\fileinfo{filename} more text
\fileinfo{ directory  }
\fileinfo{FULL} should match because matching is not case sensitive
"\fileinfo{full}" nothing should be highlighted because Doxygen fails to parse this correctly.
\fileinfo {full} Only fileinfo should be highlighted because space is not allowed
\fileinfo{full,name} Nothing matches because two options are not allowed.
\fileinfo{file} Nothing matches because "file" is an unknown option.
Some text \lineinfo more text
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
\module
@module my_module
\name
\name some group title
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
\showdate "" Format even empty ""
\showdate Do not format the "Do" because not quoted
\showdate  
\showdate
	Nothing to format on this line
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
\important Some important text
\invariant Some text
\note Some note
\par User defined paragraph:
\par
\par:  some title
\param[out] dest The memory area to copy to.
\param[in]  src  The memory area to copy from.
\param[in]  n    The number of bytes to copy
\param[in,out] p In and out param
\param	   [ in  ] 	 test Description
\param[ out 	 ]  test Description
\param	[in,	out ] test Description
\param[out,in] test Description
\param [out, in] test Description
\param  [ out, in	] test Description
\param p some param
\param x,y,z Coordinates of the position in 3D space.
\param
\param  
\param  [ out , in	]
\param  [ out, in]  
\param[outin] outinParam Some description
\param[out  in ] outinParam Some description
\param[ inout] outinParam Some description
\param[ in out] outinParam Some description
\param > The ">" should not be formatted because it is not a valid parameter.
\param [in]someParam Only param gets formatted, but not "someParam" because of missing space after ]. Doxygen parses it incorrectly.
\param[ out Nothing should be formatted because of invalid syntax (missing closing bracket).
\param[invalid] Nothing should be formatted because "invalid" is not allowed.
\param[out,invalid] Nothing should be formatted because "invalid" is not allowed.
\param[in,in] No formatting because repetition of "in" is not allowed for param.
\param[out,in,out] No formatting because repetition of "out" is not allowed for param.
\param[OUT] param Nothing should be formatted because the option is case-sensitive.
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
Some text \anchor some_word
\cite  some_label
Some text \link link_obj some text \endlink
\endlink more text
This page contains \ref subsection1 and \ref subsection2: Some more \ref subsection3.
See \ref link_text "some text" and more. See \ref someFunc() "some text 2" and more.
\ref link_text3 "some"
\ref link_text5 not formatted because no quotes
\ref Class::Func()
Some \ref Class::cls::func() text
Text \ref Class.Func() more text (the point should also work)
See \ref Class.Func(). The last point should not be matched.
See \ref Class::Func(double,int), the last comma should not be matched.
\ref func(double, int), bla. The last comma should not be matched.
Bla (cf. \ref Class::Func()) bla. The closing ")" should not be matched.
Bla (cf. \ref Class::Func(int, double, cls::f)) bla
\ref func()() the second "()" should not be matched.
\ref Class1.:Func Here, only "Class" should be matched because of the incorrect indirection
\ref Class2:.Func Here, only "Class" should be matched because of the incorrect indirection
\ref Class3:Func Here, only "Class" should be matched because of the incorrect indirection
\ref "foo" Do not match quotation, only keyword
\ref
  nothing to match
\ref match
  "but do not match this"
\ref  
\ref
Some text \refitem some_name more text
\secreflist
\endsecreflist
- \subpage intro
- \subpage advanced "Advanced usage"
\tableofcontents
\tableofcontents some text
\tableofcontents{html}
\tableofcontents{latex:1}
\tableofcontents{XML: 6}some text without space after } should work
\tableofcontents{xml , html : 2 , latex,docbook:3} should work
\tableofcontents{ XML } should work
\tableofcontentsNOTHING nothing highlighted because of missing space.
\tableofcontents {xml} Only partial highlight because space before { is not allowed.
\tableofcontents{xml:unknownLevel} Only partial highlight because of invalid level.
\tableofcontents{xml:0} Only partial highlight because of invalid level.
\tableofcontents{xml:-1} Only partial highlight because of invalid level.
\tableofcontents{xml:7} Only partial highlight because of invalid level.
\tableofcontents{unknown} Only partial highlight because of unknown option.
\tableofcontents{unknown:3} Only partial highlight because of unknown option.
\section sec An example section
\section    
	Nothing to format on this line
\section
  Nothing to format on this line
\subsection sec_2
\subsubsection sec An example section
\paragraph sec An example section
\subparagraph subpara An example subparagraph
\subsubparagraph subsubpara  An example subsubparagraph


--- Commands for displaying examples ---
\dontinclude include_test.cpp
\dontinclude{lineno} some dir\include_test.cpp
\dontinclude{ strip }
\dontinclude{nostrip}  
\include
\include include_test.cpp
\include{doc}
\include{lineno} some dir\include_test.cpp  
\include{doc} "some dir\include_test.cpp"
\include{local} "some dir\include_test.cpp"
\include{strip} include_test.cpp
\include{nostrip} include_test.cpp
\include{raise = 1 } include_test.cpp
\include{prefix = some great.prefix} include_test.cpp
\include{lineno,local}
\include{doc,local} include_test.cpp
\include{local,lineno, } include_test.cpp
\include{ local, doc }  include_test.cpp
\include{}
\include {local}  Only partial highlight because whitespace after include not allowed
\include{local,unknownlocal} Only partial highlight because "unknownlocal" is unknown.
\include{doc}} No highlight because of mismatching braces
\include{doc,raise=6} Only partial highlight because max. raise level is 5.
\include{doc,raise=99} Only partial highlight because max. raise level is 5.
\include{local,STRIP} Only partial highlight because option is case-sensitive.
\includelineno  include_test.cpp
\includedoc   include_test.cpp
\includedoc{doc}  include_test.cpp
\includedoc{ raise =5 }  include_test.cpp
\includedoc{prefix=pref, raise=5 }  include_test.cpp
\includedoc{local} Only partial highlight because "local" is not supported.
\line example();
\skip main
\skipline Include_Test t;
\until {
\snippet
\snippet snippets/example.cpp
\snippet{doc}
\snippet snippets/example.cpp Adding a resource
\snippet{lineno} snippets/example.cpp resource
\snippet{doc} example.cpp resource
\snippet{trimleft} example.cpp resource
\snippet{local} example.cpp resource
\snippet{strip} example.cpp resource
\snippet{nostrip} example.cpp resource
\snippet{raise=0} example.cpp resource
\snippet{prefix=fn_} example.cpp resource
\snippet{prefix = some prefix , doc} example.cpp resource
\snippet{lineno,local} example.cpp resource
\snippet{doc,local} example.cpp resource
\snippet{trimleft,local} example.cpp resource
\snippet{local,lineno} example.cpp resource
\snippet{local ,doc} example.cpp resource
\snippet{ local, trimleft  }  example.cpp resource
\snippet{local,} example.cpp  resource
\snippet{doc,  } example.cpp resource
\snippet{} example.cpp resource
\snippet{  } example.cpp resource
\snippet {local} example.cpp Only partial highlight because whitespace after snippet not allowed
\snippet{local,unknownlocal} example.cpp Only partial highlight because "unknownlocal" is unknown.
\snippet{doc, prefix=some prefix, unknownlocal} example.cpp Only partial highlight because "unknownlocal" is unknown.
\snippet{doc}} example.cpp No highlight because of mismatching braces
\snippet{doc,raise=6} example.cpp Only partial highlight because max. raise level is 5.
\snippet{doc,raise=99} example.cpp Only partial highlight because max. raise level is 5.
\snippet{DOC} example.cpp Only partial highlight because the options are case-sensitive.
\snippetlineno  snippets/example.cpp resource
\snippetdoc  example.cpp Some resource
\snippetdoc{doc}  example.cpp resource
\snippetdoc{ raise =5 }  example.cpp resource
\snippetdoc{prefix=pref, raise=5 }  example.cpp resource
\snippetdoc{local} example.cpp Only partial highlight because "local" is not supported.
\verbinclude some dir\include_test.cpp
\htmlinclude some dir\html.cpp
\htmlinclude some [block].cpp allowed
\htmlinclude[block]   html.cpp allowed
\htmlinclude[   block   ]
\htmlinclude[block ] allowed
\htmlinclude[block]] not allowed
\htmlinclude[unknown] will be filtered-out separately
\htmlinclude[   block   ]Missing space after ] not allowed
\htmlinclude  [block]  Space before [ not allowed
\htmlinclude[BLOCK] Only partial highlight because option is case-sensitive.
\latexinclude  some dir\tex.cpp
\rtfinclude	some dir\rtf.cpp
\maninclude some dir\man.cpp
\docbookinclude some dir\doc.cpp
\xmlinclude some dir\xml.cpp


--- Commands for visual enhancements ---
the \a x and @a y::p coordinates are used to
the \b x and @b y::p coordinates are used to
the \c x and @c y::p coordinates are used to
the \p x and @p y::p coordinates are used to
  \p ::thing
\p
  nothing to format on this line
\p
nothing to format on this line
Format (the next)\p thing despite missing space.
\arg AlignLeft left alignment.
\li
\code{.py} \endcode
Text \code{.c} someCode \endcode
\code{.py}class \endcode
\code{.cpp}
class Cpp {};
\endcode
\code{.c++}
class Cpp2 {};
\endcode
\code   {.unparsed} space before { is ignored.
Show this as-is please
\endcode
\code{.CS}  case-insensitive match
\endcode
\code
Some code
\endcode
\code 
\endcode
\code{unknown} \endcode
\code{.unknown} \endcode
\code{ .py} no whitespace allowed in braces \endcode
\code{.py } no whitespace allowed in braces \endcode
@copydoc MyClass::myfunction(type1,type2)
Some text @copydoc MyClass::myfunction() more text.
\brief \copybrief foo()
\details \copydetails foo()
\docbookonly
\enddocbookonly
\dot "foo test" width=200cm height=1cm
\dot width=2\textwidth   height=1cm
\dot  "foo"  width=200cm
\dot "foo test" height=\textwidth shouldNotMatch
bla \dot "foo test"
\dot
\dot shouldNotMatch width=200cm height=1cm
\enddot
Some text \emoji :smile: more text \emoji left_luggage
@msc "foo test" width=200cm height=1cm
\msc
\endmsc
\startuml
@startuml{myimage.png} "Image Caption" width=200cm height=1cm
  Alice -> Bob : Hello
@startuml{json, myimage.png} "Image Caption"
@startuml{json}
@enduml
\dotfile filename "foo test" width=200cm height=1cm
\dotfile "file name" "foo  test" width=200cm height=1cm
\dotfile filename
\dotfile "file name"
\dotfile "file name" "foo test" 
\dotfile filename "foo test" 
\dotfile
\mscfile file_name.msc "test" width=200cm
\diafile "path\with space\file_name.dia" width=200cm
\plantumlfile "some file" "some caption" height=10cm
\plantumlfile "another file" invalid caption because no quotes
\plantumlfile file invalid caption because no quotes
... Project name = \doxyconfig PROJECT_NAME ...
this is a \e really good example
this is a \em x good example
\htmlonly
\htmlonly   
\htmlonly This text after the command is not part of the command anymore, only htmlonly should be highlighted.
\htmlonly[block]  
\htmlonly[ block ]
\htmlonly[block] Should not get confused because of the following [block].
\htmlonly  [block] The block is not highlighted because space before [ is not allowed.
\htmlonly[BLOCK] The block is not highlighted because the option is case sensitive.
\htmlonly[unknown] The unknown should not be highlighted.
\endhtmlonly
\latexonly	
\endlatexonly	
\manonly
\endmanonly
\rtfonly
\endrtfonly
\verbatim
\endverbatim
\xmlonly
\endxmlonly
The distance between \f$(x_1,y_1)\f$ and \f$(x_2,y_2)\f$ is 
  \f$\sqrt{(x_2-x_1)^2+(y_2-y_1)^2}\f$. Another \f$formula\f$
\f((x^2+y^2)^2\f)
\f[formula\f]
   \f{eqnarray*}{
        g &=& \frac{Gm_2}{r^2} \\ 
          &=& \frac{(6.673 \times 10^{-11}\,\mbox{m}^3\,\mbox{kg}^{-1}\,
              \mbox{s}^{-2})(5.9736 \times 10^{24}\,\mbox{kg})}{(6371.01\,\mbox{km})^2} \\ 
          &=& 9.82066032\,\mbox{m/s}^2
   \f}
\f{eqnarray*}
\f}
\image html
\image HTML application.jpg	 
\image latex application.eps "My application" width=10cm
\image docbook "file name.eps" width=200cm height=1cm
\image  docbook  "width/height must be lower case" Width=200cm
\image{inline,anchor:id} rtf "path with space/name.rtf"
\image{ INLINE  } xml file.xml height=1cm
\image{Anchor: some id, inline}
\image {inline} html not allowed space before {
\image{unknown} html application.jpg
\image latexs is not an allowed document type, no format of latexs
\image
  latex should not be formatted since on new line
\image  
  latex should not be formatted since on new line
\image "foo do not match"
\image foo do not match
\image
New line\n
New line \n
\n  
\n
Some mail\@address.com
Some @@ at
\~
\~english
\~ english not formatted due to space
bla \~english This is English \~dutch Dit is Nederlands \~german Dies ist Deutsch. \~ output for all languages.
Some thing\&another thing.
Word \$
Word \# word
\< word
\>
Some \%  
Some\.
Question\?mark@!.
5\=3+2
Some\::thing
  \|
Some\--word
Some\---word
Some\----word (only 3 "-" should be highlighted)
\{
\}
Foo @{ text @} foo
some "\"quoted@"" string
\\
some\\text
some @\ more text
\\cite label ("cite label" should NOT be highlighted)
@\cite label ("cite label" should NOT be highlighted)
\\\cite label ("cite label" SHOULD be highlighted)
@\\cite label ("cite label" SHOULD be highlighted)
\\\\cite label ("cite label" should NOT be highlighted)
@\\\cite label ("cite label" should NOT be highlighted)
\section cmdthrows \\cite label (the "\\cite label" should be a parameter of \section)
*/


// In the following, the closing "*/" should not be formatted:
/** \ingroup foo  */  
