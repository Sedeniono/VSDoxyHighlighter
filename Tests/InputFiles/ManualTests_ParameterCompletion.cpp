// The autocompletion of the arguments of the @param (+ variations) and @tparam
// Doxygen commands relies on official and undocumented Visual Studio APIs. So
// we cannot write automated tests properly (since they would require a running
// Visual Studio instance). This file thus collects various cases to get the
// functionality at least manually.

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

/// @param
/// @tparam
void funcDefWithDefaultArgs(const int i = 42, double d = 43.0) { }

/// @param
void funcDeclWithInlineClassDecl(class InlClassDecl param);

/// @param
void funcDefWithInlineClassDecl(class InlClassDecl & param) { }

/// @param[in]
/// @tparam
template <class templateParam, int vTempl>
int templateFunctionDefinition(double var, int iiiii, int arr[], double volatile const v)
{
  fundc();
}

// NOTE: tparam does not work, because neither the FileCodeModel nor the SemanticTokensCache is aware of the NTTP.
/// @tparam
/// \param
template <int iTempl>
void templateFunctionDeclaration(int d);

/// @tparam 
/// @param[in]
/// @param[out]
/// @param[in,out]
/// @a
/// @p
///
/// Some test
template <class tParam1, int tParam2, typename tParam3, class tParam4, int tParam5, typename tParam6>
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
    int **& param10,
    int const param11,
    int const param12)
{
  fundc();
}

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

  /// @param
  /// @tparam
  bool memberFuncWithT(T tests);

  /// @param
  /// @tparam
  bool memberFunc2(HMODULE aValue) { }

  /// @param
  /// @tparam
  template <class >
  int templateMemberFunc(U);

  /// @param
  /// @tparam
  template <class U>
  int templateMemberFunc2(U u);
};


/// @tparam
/// @param
class NonTemplateClass
{
  /// @param
  void SomeMemberFunc(int d);
};


/// @tparam
template <typename templArg>
struct TemplateStruct
{
};

/// @param
/// @tparam
struct NonTemplateStruct
{
};


// NOTE: 'i' and 'test' are not seen.
/// @tparam
template <typename templArg, auto i, int test, typename another>
struct TemplateStructDecl;


/// @tparam
/// @param
#define SOME_MACRO(x, y, zzzzz) x

/// @param
#define ANOTHER_MACRO



/// @tparam
/// @param
template <class TTT, int someInt>
using SomeUsing = void;

/// @tparam
/// @param
using SomeSpecificUsing = SomeUsing<int, 42>;
