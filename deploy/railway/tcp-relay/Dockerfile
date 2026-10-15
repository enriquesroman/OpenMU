FROM alpine:3.20
RUN apk add --no-cache socat
ENV LISTEN_PORT=45901 TARGET_HOST=openmu.railway.internal TARGET_PORT=45901
USER 1000
CMD socat TCP4-LISTEN:${LISTEN_PORT},fork,reuseaddr,keepalive TCP4:${TARGET_HOST}:${TARGET_PORT},keepalive
