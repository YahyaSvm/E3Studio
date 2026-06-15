add_library(e3_geometry STATIC
    GeometryKernel.cpp
    MeshLoader.cpp
    BRepProcessor.cpp
    GeometryUtils.cpp
)

target_include_directories(e3_geometry PUBLIC ${CMAKE_CURRENT_SOURCE_DIR})

target_link_libraries(e3_geometry PUBLIC
    e3_core
    TKernel TKMath TKBRep TKGeomBase TKGeomAlgo
    TKTopAlgo TKPrim TKBO TKShHealing TKMesh
    TKSTEP TKSTEPBase TKSTL TKIGES
    TKXSBase TKSTEPAttr TKSTEP209
)
