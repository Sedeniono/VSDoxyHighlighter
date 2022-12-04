
/// \file
/// \brief Contains various utilities

#include <cmath>

/// \defgroup MathUtils Some math utilities
/// \brief Some commonly used math functions.
/// \ingroup Utils
/// \{

/**
 * @brief Computes the 2-argument arctangent.
 * 
 * @details 
 * For `x > 0`, the result is simply `atan(y/x)` compare \ref std::atan().
 * The following image explains the result:\n
 *   @image latex "pics/atan2.eps" "2-argument arctangent" width=5cm \n
 * 
 * @param[in] y The y-coordinate
 * @param[in] x The x-coordinate
 * 
 * @warning The parameters \p x and \p y must **not** be 0 simultaneously.
 * 
 * @note The evaluation _can_ be expensive.
 */
double ATan2(double y, double x);

/// \}
