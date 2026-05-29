#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

#define MAX_DISTORTIONS 16

uniform sampler2D WorldTexture;
uniform vec2 DistortionCenters[MAX_DISTORTIONS];
uniform float DistortionRadii[MAX_DISTORTIONS];
uniform float DistortionStrengths[MAX_DISTORTIONS];
uniform float DistortionCount;
uniform float Time;

out vec4 fragColor;

void main()
{
	vec2 fc = gl_FragCoord.xy;
	vec2 sz = vec2(textureSize(WorldTexture, 0));
	int count = int(DistortionCount);

	vec2 offset = vec2(0.0);
	for (int i = 0; i < MAX_DISTORTIONS; ++i)
	{
		if (i >= count)
			break;

		float r = DistortionRadii[i];
		float d = distance(fc, DistortionCenters[i]);
		float falloff = exp(-d * d / (r * r));
		float s = DistortionStrengths[i] * falloff;

		// Rising heat-haze. A horizontal shimmer for the ripple, plus an ALWAYS-positive vertical lift
		// (0..s) whose intensity scrolls in travelling bands. The constant upward bias advects content
		// upward (reads as rising) while the scroll animates it. offset.y positive samples from below in
		// the y-down framebuffer, so content appears to move up; flip the Time sign / offset.y sign if it
		// sinks instead. Most visible over detailed content (buildings, units) — flat terrain hides it.
		float vscroll = fc.y * 0.05 - Time * 5.0;
		offset.x += sin(vscroll) * s * 0.6;
		offset.y += (0.5 + 0.5 * sin(vscroll)) * s;
	}

	vec2 maxCoord = sz - vec2(1.0);
	ivec2 sampleCoord = ivec2(clamp(fc + offset, vec2(0.0), maxCoord));
	fragColor = texelFetch(WorldTexture, sampleCoord, 0);
}
