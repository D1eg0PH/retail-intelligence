import type { ReactNode } from "react";

interface EmptyStateProps {
    title: string;
    description?: string;
    action?: ReactNode;
}

export default function EmptyState({
    title,
    description,
    action
}: EmptyStateProps) {
    return (
        <div className="rounded-xl border border-dashed border-gray-300 bg-gray-50 p-10 text-center">

            <h2 className="text-lg font-semibold text-gray-700">
                {title}
            </h2>

            {description && (
                <p className="mt-2 text-gray-500">
                    {description}
                </p>
            )}

            {action && (
                <div className="mt-6">
                    {action}
                </div>
            )}

        </div>
    );
}