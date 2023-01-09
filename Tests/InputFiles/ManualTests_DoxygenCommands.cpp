// The custom comment parser of our Visual Studio extension does not attempt
// to figure out whether a specific text span is in a comment, or a string, or is
// actually code. Doing so in general is pretty complicated. Instead, we use
// a hack and rely on the default Visual Studio formatter. Unfortunately, that
// means we cannot write automated tests because it would require a running
// Visual Studio instance. Thus, this file here contains some stuff to check
// cases where no formatting is expected by our extension manually, and also
// some cases where we suspect that the employed hack might fail.

#include <utility>

// No formatting of the commands in the string expected:
char const t1[] = "foo @p test foo, \cite test foo";
char const t2[] = "foo \"no format\" also";
char const t3[] = R"(No *formatting* at all.
	/**
		@brief No formatting \p in here
	*/
	)";

// No formatting of double underscores in code expected:
char const _t4[] = __FILE__;
auto const i_ = __LINE__;
int const a = (int) _t4, i_;

// No formatting of starts as italic expected:
double var = 5 *2; // foo* foo
double m = 5 *var* 6;

// var1 should not be formatted, but var2 should actually be italic:
std::pair<double /*var1*/, int /* *var2* */> p1;

// No formatting expected (especially since the "int" is not a comment):
std::pair<double /*foo _some */, int /* stuff_ */> p2;

/// Some comment that goes \
	**continues** on the next line and the next \
	@details and thus all here is expected to be formatted. \
	/* @p TEST */ even this should be formatted if "/*" formatting is disabled


// We expect that formatting does occur, despite the nested style 
// (if "/*" formatting is disabled):

/// Foo /* text @p TEST text */ foo


