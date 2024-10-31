import { Router, text, withParams } from 'itty-router';

const docs = `# /v1/magnet/:encodedMagnet

Redirects to the specified magnet link. Primarily for linking to magnet links on Discord with a clickable link (especially useful for components).
encodedMagnet is expected to be a base64 encoded magnet link.

---

Source code at https://github.com/SquirrelKiev/Asahi, under the redirector-worker directory.`;

const router = Router({
    before: [withParams],
    catch: text,
})
    .get('/', () => text(docs))
    .get('/v1/magnet/:encodedMagnet', ({ encodedMagnet }) => {
        try {
            const decodedMagnet = atob(encodedMagnet);

            if (!decodedMagnet.startsWith('magnet:')) {
                throw new Error('Invalid magnet link.');
            }

            return Response.redirect(decodedMagnet, 303);
        } catch {
            return text('Not a valid magnet link!', { status: 400 });
        }
    })
    .all('*', () => text('404 - Not found', { status: 404 }));

export default router;
