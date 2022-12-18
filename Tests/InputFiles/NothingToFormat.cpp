/**
 * @params Nothing should be formatted because params is not a keyword
 * T \brief Nothing should be formatted because keyword is not at the start
 T \note Nothing should be formatted because keyword is not at the start
 @param@warning Nothing should be formatted because keywords are not separate
 @param: Nothing to format because of the ":", which is not allowed
 \briefs Incorrect keyword
 No format because \refs is unknown
 Nothing to\ref format because no space before command.
 Nothing to\p format because no space before command.
 \classs No formatting because of unknown command
*/

/// T \note Nothing should be formatted because keyword is not at the start

/// @Brief Nothing to format because of capital B. doxygen is case sensitive.


/***************** Nothing bold or italic
*
**
***
****
*****
******
*******
*/


/** Nothing bold or italic
_
__
___
____
_____
______
_______
*/

/** Nothing italic
char *Fn_Test
*/

/** Nothing bold
char *Fn_Test
**/


a *= b;  /* 0.6, originally 0.7 */
if (/* ??? */(false)/* ??? */) {}
std::pair<double /*name*/, int> p;
std::pair<double /* name*/, int> p;
std::pair<double /*name */, int> p;
std::pair<double /* name */, int> p;
std::pair<double /**name**/, int> p;
std::pair<double /** name**/, int> p;
std::pair<double /**name **/, int> p;
std::pair<double /** name **/, int> p;

return *(static_cast<T*>(t));
auto ptr = int * (*)(const char*);
(double*)(void*)p;
(double*)(void* )p;
(double *)(void*)p;
(double *)(void* )p;
(double * )(void* )p;
(double * )(void * )p;
(double *)(void * )p;

