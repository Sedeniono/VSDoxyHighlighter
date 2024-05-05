// The autocompletion of the arguments of the @param (+ variations) and @tparam
// Doxygen commands relies on official and undocumented Visual Studio APIs. So
// we cannot write automated tests properly (since they would require a running
// Visual Studio instance). This file thus collects various cases to get the
// functionality at least manually.

#include <functional>
#include <concepts>

//============================================================================
// Functions
//============================================================================

/// @param
void funcDeclarationWithParam(int foo);

/// @param
int funcDeclarationWithoutParam();

/// @param
void funcDefinitionWithParam(int foo) { }

/// @param
int funcDefinitionWithoutParam() { }

/// @param
void funcDeclarationWithUnnamedParams(int, double);

/// @param
void funcDeclWithDefaultArgs(const int i = 42, double d = 43.0);

/// @tparam
/// @param someInt
/// @param
void funcDefWithDefaultArgs(const int someInt = 42, double someDouble = 43.0) { }

/// @param
void funcDeclWithInlineClassDecl(class InlClassDecl param);

/// @param
void funcDefWithInlineClassDecl(class InlClassDecl & param) { }

/// @param[in]
/// @tparam
template <class templateParam, int vTempl>
int templateFunctionDecl(double var, int iiiii, int arr[], double volatile const v);

/// @param[in]
/// @tparam
template <class templateParam, int vTempl>
int templateFunctionDef(double var, int iiiii, int arr[], double volatile const v)
{
  fundc();
}

// NOTE: tparam does not work, because neither the FileCodeModel nor the SemanticTokensCache is aware of the NTTP.
/// @tparam
/// \param
template <int iTempl>
void templateFunctionDeclaration(int d);


/// @tparam tParam2 some description
/// @tparam tParam3 more description
/// @param[in] param2 desc
/// test
/// test @p
/// @param[out] param3
/// 
/// @param[in,out] param9
/// @param param10
/// @a
/// @p
///
/// Some test
template <class tParam1, int tParam2, 
    typename tParam3, class tParam4, 
    int tParam5, typename tParam6>
int ManyParameters(
    tParam1 param1,
    int param2,
    unsigned int param3,
    TestClass const & param4,
    int (*param5)(double, short),
    class InlCls param6,
    int param7,
    int * param8,
    int & param9,
    // Some comment in the middle
    int **& param10,
    int const param11,
    int const param12)
{
  fundc();
}


/// @param 
bool ComplicatedParametersDef(
    std::function<int(double p1, short p2)> func1,
    std::function<int(long double const **,
            size_t const,
            bool *&)> 
        func2WithLineBreaks,
    decltype([](double volatile &, int){
                return false;}
            ) param3,
    ...);

/// @param 
bool ComplicatedParametersDef(
    std::function<int(double p1, short p2)> func1,
    std::function<int(long double const **,
            size_t const,
            bool *&)> 
        func2WithLineBreaks,
    decltype([](double volatile &, int){
                return false;}
            ) param3,
    ...)
{
  return false;
}


/// @param
void OnlyEllipsisDecl(...);

/// @param
void OnlyEllipsisDef(...);


/// @tparam
/// @param
template <class... ArgsT>
void ParameterPackDecl(long double var1, ArgsT &&... args);

/// @tparam
/// @param
template <class... ArgsT>
void ParameterPackDef(long double var1, ArgsT &&... args)
{
}


/// @tparam
/// @param
template <std::integral T>
void TemplateWithConceptDecl(T const & param);

/// @tparam
/// @param
template <std::integral T>
void TemplateWithConceptDef(T const & param)
{
}


#define INT_MACRO int

/// @tparam
/// @param
template <INT_MACRO templateParam>
void ParameterTypeAsMacroDecl(INT_MACRO param);

/// @tparam
/// @param
template <INT_MACRO templateParam>
void ParameterTypeAsMacroDecl(INT_MACRO param)
{
}


/// @param
void InvalidStuffDecl(some invalid parameter, another invalid parameter 42);

/// @param
void InvalidStuffDef(some invalid parameter, another invalid parameter2 42) { }


//============================================================================
// Classes/structs
//============================================================================

/**
 * @brief
 * @tparam
 * @param
 *
 * \brief adasd
 */
template <class templClsArg, unsigned someInt>
class TemplateClass
{
  int m;

  /// @tparam
  /// @param
  bool memberFuncWithT(T tests);

  /// @tparam
  /// @param
  bool memberFunc2(HMODULE aValue) { }

  /// @tparam
  /// @param
  template <class >
  int templateMemberFunc(U);

  /// @tparam
  /// @param
  template <class U>
  int templateMemberFunc2(U u);

  /// @param
  int templateMemberFunc3WithTypename(typename templClsArg::SomeType param);

  /// @param
  bool operator==(TemplateClass const & rhs);

  /// @tparam
  /// @param
  template <class cls1, unsigned iT>
  bool operator==(TemplateClass<cls1, iT> const & rhs) const;

  /// @param
  auto operator<=>(TemplateClass const & rhs) const = default;
};

/// @tparam
/// @param
template <class templClsArg, unsigned someInt>
bool operator!=(TemplateClass<templClsArg, someInt> const & lhs, TemplateClass<templClsArg, someInt> const & rhs);

/// @tparam
/// @param
template <class templClsArg, unsigned someInt>
bool operator!=(TemplateClass<templClsArg, someInt> const & lhs, TemplateClass<templClsArg, someInt> const & rhs)
{
}


/// @tparam
/// @param
class NonTemplateClass
{
  /// @param
  void SomeMemberFunc(int d);

  /// @param
  bool operator==(NonTemplateClass const & rhs) const;

  /// @param
  auto operator<=>(NonTemplateClass const & rhs) const = default;
};

/// @param
bool operator!=(NonTemplateClass const & lhs, NonTemplateClass const & rhs);

/// @param
bool operator!=(NonTemplateClass const & lhs, NonTemplateClass const & rhs) { }


/// @tparam
template <typename templArg>
struct TemplateStruct
{
};

/// @tparam
/// @param
struct NonTemplateStruct
{
};


// NOTE: 'i' and 'test' are not seen.
/// @tparam
/// @param
template <typename templArg, auto i, int test, typename another>
struct TemplateStructDecl;


/// @tparam
/// @param
template <class... ArgsT>
class ClassWithParameterPackDecl;

/// @tparam
/// @param
template <class... ArgsT>
class ClassWithParameterPackDef
{
};


/// @tparam 
/// @param
template <std::integral T>
class ClassWithConceptDecl;

/// @tparam
/// @param
template <std::integral T, class T2>
class ClassWithConceptDef
{
};


/// @tparam
/// @param
template <INT_MACRO templateParam>
class ClassWithMacroDecl;

/// @tparam
/// @param
template <INT_MACRO templateParam>
class ClassWithMacroDef
{
};



//============================================================================
// Using alias
//============================================================================

/// @tparam
/// @param
template <class TTT, int someInt>
using SomeUsing = void;

/// @tparam
/// @param
using SomeSpecificUsing = SomeUsing<int, 42>;

/// @tparam
/// @param
template <std::integral T>
using SomeUsingWithConcept = void;


//============================================================================
// Macros
//============================================================================

/// @tparam
/// @param y
/// @param zzzzz
/// @param x
#define SOME_MACRO(x, y, zzzzz) x

/// @param
#define ANOTHER_MACRO

/// @param
#define VARIADIC_MACRO(param1, ...)

/// @param
/// @param
#define MACRO_WITH_LINE_BREAKS(param1, \
    param2,\
    param3)
