// The autocompletion of the arguments of the @param (+ variations) and @tparam
// Doxygen commands relies on official and undocumented Visual Studio APIs. So
// we cannot write automated tests properly (since they would require a running
// Visual Studio instance). This file thus collects various cases to get the
// functionality at least manually.

#include <concepts>
#include <functional>
#include <optional>

class SomeClass
{
};


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

// Currently broken: @tparam for vTempl
// Currently broken: @param for arr does not show []
/// @param[in]  
/// @tparam  
template <class templateParam, int vTempl>
int templateFunctionDecl(double var, int iiiii, int arr[], double volatile const v);

// Currently broken: @param for arr does not show []
/// @param[in]
/// @tparam
template <class templateParam, int vTempl>
int templateFunctionDef(double var, int iiiii, int arr[], double volatile const v)
{
  fundc();
}

// Currently broken: tparam does not work, because neither the FileCodeModel nor the SemanticTokensCache is aware of the NTTP.
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


// Currently broken: @param does not show the type for func2WithLineBreaks and param3
/// @param
[[nodiscard]] bool ComplicatedParametersDef(
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
[[nodiscard]] bool ComplicatedParametersDef(
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

// Currently broken: @tparam does not show templateParam
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


// Currently broken: @tparam incorrectly shows SomeClass
/// @tparam
/// @param
SomeClass FuncWithReturnTypeDecl(SomeClass param1, int param2);

/// @tparam
/// @param
SomeClass FuncWithReturnTypeDef(SomeClass param1, int param2) { }


// Currently broken: @tparam incorrectly shows SomeClass
/// @tparam
/// @param
template <class T>
SomeClass TemplateFuncWithReturnTypeDecl(SomeClass param1, int param2);

/// @tparam
/// @param
template <class T>
SomeClass TemplateFuncWithReturnTypeDef(SomeClass param1, int param2) { }


// Currently broken: @tparam incorrectly shows SomeClass and InlClass
/// @tparam
/// @param 
std::pair<SomeClass, class InlClass> FuncWithReturnType2Decl(SomeClass param1, int param2);

/// @tparam
/// @param
std::pair<SomeClass, class InlClass> FuncWithReturnType2Def(SomeClass param1, int param2) { }


/// @tparam
/// @param
constexpr int ConstExprFunc(int v) { return v; }

// Currently broken: @param does not show param2
/// @tparam
/// @param
void FuncDeclWithDefaultParamFromFunc(int param1 = ConstExprFunc(42), double param2 = 1.0);

/// @tparam
/// @param
void FuncDefWithDefaultParamFromFunc(int param1 = ConstExprFunc(42), double param2 = 1.0) { }


// Currently broken: @tparam does not show iT
// Currently broken: @param shows neither param1 nor param2
/// @tparam
/// @param
template <class T, int iT = ConstExprFunc(42)>
void TemplFuncWithDefaultParamFromFunc(int param1 = ConstExprFunc(42), double param2 = 1.0);

// Currently broken: @param shows neither param1 nor param2
/// @tparam
/// @param
template <class T, int iT = ConstExprFunc(42)>
void TemplFuncWithDefaultParamFromFunc(int param1 = ConstExprFunc(42), double param2 = 1.0) { }



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


  /// @tparam
  /// @param
  std::pair<SomeClass, class InlClass> FuncWithReturnType2Decl(SomeClass param1, int param2);

  /// @tparam
  /// @param
  std::pair<SomeClass, class InlClass> FuncWithReturnType2Def(SomeClass param1, int param2) { }
  
  
  /// @tparam
  /// @param 
  template <class T>
  SomeClass TemplateFuncWithReturnTypeDecl(SomeClass param1, int param2);
  
  /// @tparam
  /// @param
  template <class T>
  SomeClass TemplateFuncWithReturnTypeDef(SomeClass param1, int param2) { }


  /// @param
  bool operator==(TemplateClass const & rhs);

  /// @tparam
  /// @param
  template <class cls1, unsigned iT>
  bool operator==(TemplateClass<cls1, iT> const & rhs) const;

  /// @param
  auto operator<=>(TemplateClass const & rhs) const = default;
};

// Currently broken: @tparam does not show someInt
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
  [[nodiscard]] virtual bool SomeVirtualMemberFunc(double d) override;

  /// @tparam
  /// @param
  TemplateClass<int, 42> SomeMemberFuncReturningType(int param);

  /// @param
  bool operator==(NonTemplateClass const & rhs) const;

  /// @tparam
  /// @param
  std::strong_ordering operator<=>(NonTemplateClass const & rhs) const = default;
};

/// @param
bool operator!=(NonTemplateClass const & lhs, NonTemplateClass const & rhs);

/// @param
bool operator!=(NonTemplateClass const & lhs, NonTemplateClass const & rhs) { }


/// @tparam
/// @param
template <typename templArg>
struct TemplateStruct
{
};

/// @tparam
/// @param
struct NonTemplateStruct
{
};


// Currently broken: @tparam does not show 'i' and 'test'
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


// Currently broken: @tparam does not show templateParam
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

// Currently broken: @param shows funcParam1 and funcParam2 of the next function.
/// @tparam
/// @param
template <std::integral T>
using SomeUsingWithConcept = void;


//============================================================================
// Macros
//============================================================================

// Currently broken: @param shows funcParam1 and funcParam2 of the next function.
/// @tparam
/// @param x
/// @param
#define SOME_MACRO(x, y, zzzzz) x

// Currently broken: @param shows funcParam1 and funcParam2 of the next function.
/// @param
#define ANOTHER_MACRO

// Currently broken: @param shows funcParam1 and funcParam2 of the next function.
/// @param
#define VARIADIC_MACRO(param1, ...)

// Currently broken: @param shows funcParam1 and funcParam2 of the next function.
/// @param
/// @param
#define MACRO_WITH_LINE_BREAKS(param1, \
    param2,\
    param3)

// This is here to check that autocompleting in the above macros does
// not find the function parameters.
void funcAfterMacro(int funcParam1, double funcParam2);
