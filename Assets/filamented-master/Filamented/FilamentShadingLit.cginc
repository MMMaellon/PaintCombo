#ifndef FILAMENT_SHADING_LIT
#define FILAMENT_SHADING_LIT

#include "FilamentMaterialInputs.cginc"
#include "FilamentCommonMath.cginc"
#include "FilamentCommonGraphics.cginc"
#include "FilamentCommonLighting.cginc"
#include "FilamentCommonMaterial.cginc"
#include "FilamentCommonShading.cginc"


#if defined(SHADING_MODEL_SUBSURFACE)
    #include "FilamentShadingSubsurface.cginc"
#elif defined(SHADING_MODEL_CLOTH)
    #include "FilamentShadingCloth.cginc"
#else
    #include "FilamentShadingStandard.cginc"
#endif

#include "FilamentLightIndirect.cginc"
#include "FilamentShadingLit.cginc"
#include "FilamentLightDirectional.cginc"
#include "FilamentLightPunctual.cginc"
#include "FilamentLightLTCGI.cginc"

#include "UnityStandardInput.cginc"

//------------------------------------------------------------------------------
// Lighting
//------------------------------------------------------------------------------

#if defined(BLEND_MODE_MASKED)
float computeMaskedAlpha(float a) {
    // Use derivatives to smooth alpha tested edges
    return (a - getMaskThreshold()) / max(fwidth(a), 1e-3) + 0.5;
}

float computeDiffuseAlpha(float a) {
    // If we reach this point in the code, we already know that the fragment is not discarded due
    // to the threshold factor. Therefore we can just output 1.0, which prevents a "punch through"
    // effect from occuring. We do this only for TRANSLUCENT views in order to prevent breakage
    // of ALPHA_TO_COVERAGE.
    return (NEEDS_ALPHA_CHANNEL == 1.0) ? 1.0 : a;
}

void applyAlphaMask(inout float4 baseColor) {
    baseColor.a = computeMaskedAlpha(baseColor.a);
    if (baseColor.a <= 0.0) {
        discard;
    }
}

#else // not masked

float computeDiffuseAlpha(float a) {
#if defined(BLEND_MODE_TRANSPARENT) || defined(BLEND_MODE_FADE)
    return a;
#else
    return 1.0;
#endif
}

void applyAlphaMask(inout float4 baseColor) {}

#endif

#if defined(GEOMETRIC_SPECULAR_AA)
float normalFiltering(float perceptualRoughness, const float3 worldNormal) {
    // Kaplanyan 2016, "Stable specular highlights"
    // Tokuyoshi 2017, "Error Reduction and Simplification for Shading Anti-Aliasing"
    // Tokuyoshi and Kaplanyan 2019, "Improved Geometric Specular Antialiasing"

    // This implementation is meant for deferred rendering in the original paper but
    // we use it in forward rendering as well (as discussed in Tokuyoshi and Kaplanyan
    // 2019). The main reason is that the forward version requires an expensive transform
    // of the half vector by the tangent frame for every light. This is therefore an
    // approximation but it works well enough for our needs and provides an improvement
    // over our original implementation based on Vlachos 2015, "Advanced VR Rendering".

    float3 du = ddx(worldNormal);
    float3 dv = ddy(worldNormal);

    float variance = _specularAntiAliasingVariance * (dot(du, du) + dot(dv, dv));

    float roughness = perceptualRoughnessToRoughness(perceptualRoughness);
    float kernelRoughness = min(2.0 * variance, _specularAntiAliasingThreshold);
    float squareRoughness = saturate(roughness * roughness + kernelRoughness);

    return roughnessToPerceptualRoughness(sqrt(squareRoughness));
}
#endif

void getCommonPixelParams(const MaterialInputs material, inout PixelParams pixel) {
    float4 baseColor = material.baseColor;
    applyAlphaMask(baseColor);

#if defined(BLEND_MODE_FADE) && !defined(SHADING_MODEL_UNLIT)
    // Since we work in premultiplied alpha mode, we need to un-premultiply
    // in fade mode so we can apply alpha to both the specular and diffuse
    // components at the end
    unpremultiply(baseColor);
#endif

#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    // This is from KHR_materials_pbrSpecularGlossiness.
    float3 specularColor = material.specularColor;
    float metallic = computeMetallicFromSpecularColor(specularColor);

    pixel.diffuseColor = computeDiffuseColor(baseColor, metallic);
    pixel.f0 = specularColor;
#elif !defined(SHADING_MODEL_CLOTH)
    pixel.diffuseColor = computeDiffuseColor(baseColor, material.metallic);
    #if !defined(SHADING_MODEL_SUBSURFACE) && (!defined(MATERIAL_HAS_REFLECTANCE) && defined(MATERIAL_HAS_IOR))
    float reflectance = iorToF0(max(1.0, material.ior), 1.0);
#else
    // Assumes an interface from air to an IOR of 1.5 for dielectrics
    float reflectance = computeDielectricF0(material.reflectance);
#endif
    pixel.f0 = computeF0(baseColor, material.metallic, reflectance);
#else
    pixel.diffuseColor = baseColor.rgb;
    pixel.f0 = material.sheenColor;
#if defined(MATERIAL_HAS_SUBSURFACE_COLOR)
    pixel.subsurfaceColor = material.subsurfaceColor;
#endif
#endif

#if !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
#if defined(HAS_REFRACTION)
    // Air's Index of refraction is 1.000277 at STP but everybody uses 1.0
    const float airIor = 1.0;
#if !defined(MATERIAL_HAS_IOR)
    // [common case] ior is not set in the material, deduce it from F0
    float materialor = f0ToIor(pixel.f0.g);
#else
    // if ior is set in the material, use it (can lead to unrealistic materials)
    float materialor = max(1.0, material.ior);
#endif
    pixel.etaIR = airIor / materialor;  // air -> material
    pixel.etaRI = materialor / airIor;  // material -> air
#if defined(MATERIAL_HAS_TRANSMISSION)
    pixel.transmission = saturate(material.transmission);
#else
    pixel.transmission = 1.0;
#endif
#if defined(MATERIAL_HAS_ABSORPTION)
#if defined(MATERIAL_HAS_THICKNESS) || defined(MATERIAL_HAS_MICRO_THICKNESS)
    pixel.absorption = max(0.0, material.absorption);
#else
    pixel.absorption = saturate(material.absorption);
#endif
#else
    pixel.absorption = 0.0;
#endif
#if defined(MATERIAL_HAS_THICKNESS)
    pixel.thickness = max(0.0, material.thickness);
#endif
#if defined(MATERIAL_HAS_MICRO_THICKNESS) && (REFRACTION_TYPE == REFRACTION_TYPE_THIN)
    pixel.uThickness = max(0.0, material.microThickness);
#else
    pixel.uThickness = 0.0;
#endif
#endif
#endif
}

void getSheenPixelParams(const ShadingParams shading, const MaterialInputs material, inout PixelParams pixel) {
#if defined(MATERIAL_HAS_SHEEN_COLOR) && !defined(SHADING_MODEL_CLOTH) && !defined(SHADING_MODEL_SUBSURFACE)
    pixel.sheenColor = material.sheenColor;

    float sheenPerceptualRoughness = material.sheenRoughness;
    sheenPerceptualRoughness = clamp(sheenPerceptualRoughness, MIN_PERCEPTUAL_ROUGHNESS, 1.0);

#if defined(GEOMETRIC_SPECULAR_AA)
    sheenPerceptualRoughness =
            normalFiltering(sheenPerceptualRoughness, shading.geometricNormal);
#endif

    pixel.sheenPerceptualRoughness = sheenPerceptualRoughness;
    pixel.sheenRoughness = perceptualRoughnessToRoughness(sheenPerceptualRoughness);
#endif
}

void getClearCoatPixelParams(const ShadingParams shading, const MaterialInputs material, inout PixelParams pixel) {
#if defined(MATERIAL_HAS_CLEAR_COAT)
    pixel.clearCoat = material.clearCoat;

    // Clamp the clear coat roughness to avoid divisions by 0
    float clearCoatPerceptualRoughness = material.clearCoatRoughness;
    clearCoatPerceptualRoughness =
            clamp(clearCoatPerceptualRoughness, MIN_PERCEPTUAL_ROUGHNESS, 1.0);

#if defined(GEOMETRIC_SPECULAR_AA)
    clearCoatPerceptualRoughness =
            normalFiltering(clearCoatPerceptualRoughness, shading.geometricNormal);
#endif

    pixel.clearCoatPerceptualRoughness = clearCoatPerceptualRoughness;
    pixel.clearCoatRoughness = perceptualRoughnessToRoughness(clearCoatPerceptualRoughness);

#if defined(CLEAR_COAT_IOR_CHANGE)
    // The base layer's f0 is computed assuming an interface from air to an IOR
    // of 1.5, but the clear coat layer forms an interface from IOR 1.5 to IOR
    // 1.5. We recompute f0 by first computing its IOR, then reconverting to f0
    // by using the correct interface
    pixel.f0 = lerp(pixel.f0, f0ClearCoatToSurface(pixel.f0), pixel.clearCoat);
#endif
#endif
}

void getRoughnessPixelParams(const ShadingParams shading, const MaterialInputs material, inout PixelParams pixel) {
#if defined(SHADING_MODEL_SPECULAR_GLOSSINESS)
    float perceptualRoughness = computeRoughnessFromGlossiness(material.glossiness);
#else
    float perceptualRoughness = material.roughness;
#endif

    // This is used by the refraction code and must be saved before we apply specular AA
    pixel.perceptualRoughnessUnclamped = perceptualRoughness;

#if defined(GEOMETRIC_SPECULAR_AA)
    perceptualRoughness = normalFiltering(perceptualRoughness, shading.geometricNormal);
#endif

#if defined(MATERIAL_HAS_CLEAR_COAT) && defined(MATERIAL_HAS_CLEAR_COAT_ROUGHNESS)
    // This is a hack but it will do: the base layer must be at least as rough
    // as the clear coat layer to take into account possible diffusion by the
    // top layer
    float basePerceptualRoughness = max(perceptualRoughness, pixel.clearCoatPerceptualRoughness);
    perceptualRoughness = lerp(perceptualRoughness, basePerceptualRoughness, pixel.clearCoat);
#endif

    // Clamp the roughness to a minimum value to avoid divisions by 0 during lighting
    pixel.perceptualRoughness = clamp(perceptualRoughness, MIN_PERCEPTUAL_ROUGHNESS, 1.0);
    // Remaps the roughness to a perceptually linear roughness (roughness^2)
    pixel.roughness = perceptualRoughnessToRoughness(pixel.perceptualRoughness);
}

void getSubsurfacePixelParams(const MaterialInputs material, inout PixelParams pixel) {
#if defined(SHADING_MODEL_SUBSURFACE)
    pixel.subsurfacePower = material.subsurfacePower;
    pixel.subsurfaceColor = material.subsurfaceColor;
    pixel.thickness = saturate(material.thickness);
#endif
}

void getAnisotropyPixelParams(const ShadingParams shading, const MaterialInputs material, inout PixelParams pixel) {
#if defined(MATERIAL_HAS_ANISOTROPY)
    float3 direction = material.anisotropyDirection;
    pixel.anisotropy = material.anisotropy;
    pixel.anisotropicT = normalize(mul(shading.tangentToWorld, direction));
    pixel.anisotropicB = normalize(cross(shading.geometricNormal, pixel.anisotropicT));
#endif
}

void getEnergyCompensationPixelParams(const ShadingParams shading, inout PixelParams pixel) {
    // Pre-filtered DFG term used for image-based lighting
    pixel.dfg = prefilteredDFG(pixel.perceptualRoughness, shading.NoV);

#if !defined(SHADING_MODEL_CLOTH)
    // Energy compensation for multiple scattering in a microfacet model
    // See "Multiple-Scattering Microfacet BSDFs with the Smith Model"
    pixel.energyCompensation = 1.0 + pixel.f0 * (1.0 / pixel.dfg.yyy - 1.0);
#else
    pixel.energyCompensation = (1.0);
#endif

#if !defined(SHADING_MODEL_CLOTH)
#if defined(MATERIAL_HAS_SHEEN_COLOR)
    pixel.sheenDFG = prefilteredDFG(pixel.sheenPerceptualRoughness, shading.NoV).z;
    pixel.sheenScaling = 1.0 - max3(pixel.sheenColor) * pixel.sheenDFG;
#endif
#endif
}

/**
 * Computes all the parameters required to shade the current pixel/fragment.
 * These parameters are derived from the MaterialInputs structure computed
 * by the user's material code.
 *
 * This function is also responsible for discarding the fragment when alpha
 * testing fails.
 */
void getPixelParams(const ShadingParams shading, const MaterialInputs material, out PixelParams pixel) {
    pixel = (PixelParams)0;
    getCommonPixelParams(material, pixel);
    getSheenPixelParams(shading, material, pixel);
    getClearCoatPixelParams(shading, material, pixel);
    getRoughnessPixelParams(shading, material, pixel);
    getSubsurfacePixelParams(material, pixel);
    getAnisotropyPixelParams(shading, material, pixel);
    getEnergyCompensationPixelParams(shading, pixel);
}

/**
 * This function evaluates all lights one by one:
 * - Image based lights (IBL)
 * - Directional lights
 * - Punctual lights
 *
 * Area lights are currently not supported.
 *
 * Returns a pre-exposed HDR RGBA color in linear space.
 */
float4 evaluateLights(const ShadingParams shading, const MaterialInputs material) {
    PixelParams pixel;
    getPixelParams(shading, material, pixel);

    // Ideally we would keep the diffuse and specular components separate
    // until the very end but it costs more ALUs on mobile. The gains are
    // currently not worth the extra operations
    float3 color = 0.0;

    // We always evaluate the IBL as not having one is going to be uncommon,
    // it also saves 1 shader variant
#if defined(UNITY_PASS_FORWARDBASE)
    evaluateIBL(shading, material, pixel, color);
#endif

#if defined(HAS_DIRECTIONAL_LIGHTING)
    evaluateDirectionalLight(shading, material, pixel, color);
#endif

#if defined(HAS_DYNAMIC_LIGHTING)
    evaluatePunctualLights(shading, pixel, color);
#endif

#if defined(BLEND_MODE_FADE) && !defined(SHADING_MODEL_UNLIT)
    // In fade mode we un-premultiply baseColor early on, so we need to
    // premultiply again at the end (affects diffuse and specular lighting)
    color *= material.baseColor.a;
#endif

    return float4(color, computeDiffuseAlpha(material.baseColor.a));
}

void addEmissive(const MaterialInputs material, inout float4 color) {
#if defined(MATERIAL_HAS_EMISSIVE)
    float4 emissive = material.emissive;
    //float attenuation = lerp(1.0, frameUniforms.exposure, emissive.w);
    // Exposure not supported yet
    float attenuation = emissive.w;
    color.rgb += emissive.rgb * (attenuation * color.a);
#endif
}

/**
 * Evaluate lit materials. The actual shading model used to do so is defined
 * by the function surfaceShading() found in shading_model_*.fs.
 *
 * Returns a pre-exposed HDR RGBA color in linear space.
 */
float4 evaluateMaterial(const ShadingParams shading, const MaterialInputs material) {
    float4 color = evaluateLights(shading, material);
    addEmissive(material, color);
    return color;
}

#endif // FILAMENT_SHADING_LIT